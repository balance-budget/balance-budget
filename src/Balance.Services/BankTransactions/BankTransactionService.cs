using System.Globalization;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionService : IBankTransactionService
{
    private readonly BalanceDbContext _dbContext;
    private readonly ICurrencyService _currencyService;
    private readonly IBankTransactionAttachService _attachService;
    private readonly TimeProvider _timeProvider;

    public BankTransactionService(
        BalanceDbContext dbContext,
        ICurrencyService currencyService,
        IBankTransactionAttachService attachService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyService = currencyService;
        _attachService = attachService;
        _timeProvider = timeProvider;
    }

    public async Task<PagedOutput<BankTransactionOutput>> ListAsync(
        int skip,
        int take,
        BankTransactionListFilter filter,
        string? search,
        CancellationToken cancellationToken
    )
    {
        IQueryable<BankTransaction> filtered = filter switch
        {
            BankTransactionListFilter.Inbox => _dbContext.BankTransactions.Where(b =>
                b.DismissedAt == null && b.JournalEntryId == null
            ),
            BankTransactionListFilter.Matched => _dbContext.BankTransactions.Where(b =>
                b.JournalEntryId != null
            ),
            BankTransactionListFilter.Dismissed => _dbContext.BankTransactions.Where(b =>
                b.DismissedAt != null
            ),
            BankTransactionListFilter.All => _dbContext.BankTransactions,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };

        var needle = search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            filtered = filtered.Where(b =>
                DbFunction.CaseInsensitiveLike(b.Description, $"%{needle}%")
                || (
                    b.CounterpartyName != null
                    && DbFunction.CaseInsensitiveLike(b.CounterpartyName, $"%{needle}%")
                )
            );
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var ordered = filter switch
        {
            BankTransactionListFilter.Inbox => filtered
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.CreatedAt),
            BankTransactionListFilter.Dismissed => filtered.OrderByDescending(b => b.DismissedAt),
            _ => filtered.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.CreatedAt),
        };

        var rows = await ordered
            .AsNoTracking()
            .Skip(skip)
            .Take(take)
            .Select(BankTransactionProjections.ToOutput)
            .ToListAsync(cancellationToken);

        // The Inbox hint is the only filter where a MatchingJournalEntry is meaningful (other
        // filters either show already-categorised rows or dismissed rows). Compute the predicate
        // per row via the attach service so the 7-condition logic lives in one place.
        if (filter != BankTransactionListFilter.Inbox || rows.Count == 0)
            return new PagedOutput<BankTransactionOutput>(rows, totalCount);

        var loanHints = await ComputeLoanPaymentHintsAsync(rows, cancellationToken);
        var withHints = new List<BankTransactionOutput>(rows.Count);
        foreach (var row in rows)
        {
            var hint = await _attachService.ComputeHintAsync(row.Id, cancellationToken);
            loanHints.TryGetValue(row.Id, out var loanHint);
            withHints.Add(row with { MatchingJournalEntry = hint, LoanPaymentHint = loanHint });
        }
        return new PagedOutput<BankTransactionOutput>(withHints, totalCount);
    }

    /// <summary>
    /// Loan-payment hints for one Inbox page (ADR-0025): a debit whose counterparty account
    /// number belongs to a counterparty that is some Loan's lender gets pointed at that loan's
    /// categorise mode. Resolved in one batch query; ambiguity (several loans at the same
    /// lender) yields no hint, mirroring the Attach hint's "exactly one match" stance.
    /// </summary>
    private async Task<
        Dictionary<BankTransactionId, LoanPaymentHintOutput>
    > ComputeLoanPaymentHintsAsync(
        IReadOnlyList<BankTransactionOutput> rows,
        CancellationToken cancellationToken
    )
    {
        var ibans = rows.Where(r =>
                r.Money.Amount < 0 && !string.IsNullOrWhiteSpace(r.CounterpartyAccountNumber)
            )
            .Select(r => r.CounterpartyAccountNumber!)
            .Distinct()
            .ToList();
        if (ibans.Count == 0)
            return [];

        var lenderLoans = await _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.CounterpartyId != null && b.Iban != null && ibans.Contains(b.Iban))
            .Join(
                _dbContext.Loans.AsNoTracking(),
                b => b.CounterpartyId,
                l => l.LenderCounterpartyId,
                (b, l) =>
                    new
                    {
                        b.Iban,
                        l.Id,
                        l.Name,
                    }
            )
            .ToListAsync(cancellationToken);
        if (lenderLoans.Count == 0)
            return [];

        var hintsByIban = lenderLoans
            .GroupBy(x => x.Iban!)
            .Where(g => g.Select(x => x.Id).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => new LoanPaymentHintOutput(g.First().Id, g.First().Name));

        var hints = new Dictionary<BankTransactionId, LoanPaymentHintOutput>();
        foreach (var row in rows)
        {
            if (
                row.Money.Amount < 0
                && row.CounterpartyAccountNumber is { } iban
                && hintsByIban.TryGetValue(iban, out var hint)
            )
            {
                hints[row.Id] = hint;
            }
        }

        return hints;
    }

    public async Task<Result<BankTransactionDetailOutput>> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var output = await _dbContext
            .BankTransactions.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(BankTransactionProjections.ToDetailOutput)
            .FirstOrDefaultAsync(cancellationToken);
        return output is null ? new NotFoundError("BankTransaction", id.Value.ToString()) : output;
    }

    public async Task<Result<BankTransactionOutput>> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount == 0)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionAmountZero,
                "BankTransaction Amount must be non-zero."
            );
        }

        var currency = await _currencyService.GetAsync(input.CurrencyCode, cancellationToken);
        if (currency.IsFailure)
        {
            return currency.Error;
        }

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == input.BankAccountId, cancellationToken);
        if (bankAccount is null)
        {
            return new NotFoundError("BankAccount", input.BankAccountId.Value.ToString());
        }

        if (bankAccount.AccountId is null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransactions can only be created on one of your own BankAccounts "
                    + "(BankAccount.AccountId must be set)."
            );
        }

        var rawSource = BuildManualRawSource(input);
        var rowHash = RowHasher.Hash(rawSource);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var bankTransaction = new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = input.BankAccountId,
            BookingDate = input.BookingDate,
            Money = new Money(input.Amount, input.CurrencyCode),
            Description = input.Description,
            CounterpartyName = input.CounterpartyName,
            CounterpartyAccountNumber = input.CounterpartyAccountNumber,
            RawSource = rawSource,
            RowHash = rowHash,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.BankTransactions.Add(bankTransaction);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return ToOutput(bankTransaction);
    }

    private static string BuildManualRawSource(CreateBankTransactionInput input) =>
        string.Join(
            '\n',
            "manual",
            input.BankAccountId.Value.ToString("D", CultureInfo.InvariantCulture),
            input.BookingDate.ToString("O", CultureInfo.InvariantCulture),
            input.Amount.ToString(CultureInfo.InvariantCulture),
            input.CurrencyCode.Value,
            input.Description,
            input.CounterpartyName ?? string.Empty,
            input.CounterpartyAccountNumber ?? string.Empty
        );

    public Task<Result> DeleteAsync(BankTransactionId id, CancellationToken cancellationToken) =>
        _dbContext
            .BankTransactions.Where(c => c.Id == id)
            .DeleteSingleAndCatchAsync("BankTransaction", id.Value.ToString(), cancellationToken);

    public async Task<Result<BankTransactionOutput>> DismissAsync(
        BankTransactionId id,
        string reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(reason);

        var trimmed = reason.Trim();
        if (trimmed.Length == 0)
        {
            return new InvariantError(
                ErrorCodes.RequestInvalid,
                "Dismissal reason must not be empty."
            );
        }

        var bankTransaction = await _dbContext.BankTransactions.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankTransaction is null)
        {
            return new NotFoundError("BankTransaction", id.Value.ToString());
        }

        if (bankTransaction.DismissedAt is not null)
        {
            return new ConflictError(
                ErrorCodes.BankTransactionAlreadyDismissed,
                "BankTransaction is already dismissed."
            );
        }

        if (bankTransaction.JournalEntryId is not null)
        {
            return new ConflictError(
                ErrorCodes.BankTransactionAlreadyCategorised,
                "BankTransaction has a JournalEntry and cannot be dismissed. "
                    + "Delete the JournalEntry first."
            );
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        bankTransaction.DismissedAt = now;
        bankTransaction.DismissedReason = trimmed;
        bankTransaction.UpdatedAt = now;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return ToOutput(bankTransaction);
    }

    public async Task<Result<BankTransactionOutput>> UndismissAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await _dbContext.BankTransactions.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankTransaction is null)
        {
            return new NotFoundError("BankTransaction", id.Value.ToString());
        }

        if (bankTransaction.DismissedAt is null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionNotDismissed,
                "BankTransaction is not currently dismissed."
            );
        }

        bankTransaction.DismissedAt = null;
        bankTransaction.DismissedReason = null;
        bankTransaction.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return ToOutput(bankTransaction);
    }

    private static BankTransactionOutput ToOutput(BankTransaction bankTransaction) =>
        BankTransactionProjections.ToOutputInMemory(bankTransaction);
}
