using System.Globalization;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
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
            .Select(b => new BankTransactionOutput(
                b.Id,
                b.BankAccountId,
                b.BookingDate,
                b.Money,
                b.Description,
                b.CounterpartyName,
                b.CounterpartyAccountNumber,
                b.ValueDate,
                b.Reference,
                b.MandateId,
                b.SepaCreditorId,
                b.ForeignAmount,
                b.ForeignCurrencyCode,
                b.ExchangeRate,
                b.ImporterKey,
                b.JournalEntryId,
                b.DismissedAt,
                b.DismissedReason,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        // The Inbox hint is the only filter where a MatchingJournalEntry is meaningful (other
        // filters either show already-categorised rows or dismissed rows). Compute the predicate
        // per row via the attach service so the 7-condition logic lives in one place.
        if (filter != BankTransactionListFilter.Inbox || rows.Count == 0)
            return new PagedOutput<BankTransactionOutput>(rows, totalCount);

        var withHints = new List<BankTransactionOutput>(rows.Count);
        foreach (var row in rows)
        {
            var hint = await _attachService.ComputeHintAsync(row.Id, cancellationToken);
            withHints.Add(row with { MatchingJournalEntry = hint });
        }
        return new PagedOutput<BankTransactionOutput>(withHints, totalCount);
    }

    public async Task<Result<BankTransactionDetailOutput>> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var output = await _dbContext
            .BankTransactions.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BankTransactionDetailOutput(
                b.Id,
                b.BankAccountId,
                b.BookingDate,
                b.Money,
                b.Description,
                b.CounterpartyName,
                b.CounterpartyAccountNumber,
                b.ValueDate,
                b.Reference,
                b.MandateId,
                b.SepaCreditorId,
                b.ForeignAmount,
                b.ForeignCurrencyCode,
                b.ExchangeRate,
                b.ImporterKey,
                b.JournalEntryId,
                b.DismissedAt,
                b.DismissedReason,
                b.CreatedAt,
                b.UpdatedAt,
                b.Metadata.OrderBy(m => m.Key!.Name)
                    .Select(m => new BankTransactionMetadataEntryOutput(
                        m.Key!.Name,
                        m.StringValue,
                        m.IntegerValue
                    ))
                    .ToList()
            ))
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

    public async Task<Result> DeleteAsync(BankTransactionId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .BankTransactions.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        return Result.Success;
    }

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
        new(
            bankTransaction.Id,
            bankTransaction.BankAccountId,
            bankTransaction.BookingDate,
            bankTransaction.Money,
            bankTransaction.Description,
            bankTransaction.CounterpartyName,
            bankTransaction.CounterpartyAccountNumber,
            bankTransaction.ValueDate,
            bankTransaction.Reference,
            bankTransaction.MandateId,
            bankTransaction.SepaCreditorId,
            bankTransaction.ForeignAmount,
            bankTransaction.ForeignCurrencyCode,
            bankTransaction.ExchangeRate,
            bankTransaction.ImporterKey,
            bankTransaction.JournalEntryId,
            bankTransaction.DismissedAt,
            bankTransaction.DismissedReason,
            bankTransaction.CreatedAt,
            bankTransaction.UpdatedAt
        );
}
