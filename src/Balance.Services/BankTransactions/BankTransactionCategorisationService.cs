using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionCategorisationService : IBankTransactionCategorisationService
{
    private readonly BalanceDbContext _dbContext;
    private readonly IBankAccountService _bankAccountService;
    private readonly ICounterpartyService _counterpartyService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly TimeProvider _timeProvider;

    public BankTransactionCategorisationService(
        BalanceDbContext dbContext,
        IBankAccountService bankAccountService,
        ICounterpartyService counterpartyService,
        IJournalEntryService journalEntryService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _bankAccountService = bankAccountService;
        _counterpartyService = counterpartyService;
        _journalEntryService = journalEntryService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<JournalEntryDetailOutput>> CategorizeAsync(
        BankTransactionId id,
        CategorizeBankTransactionInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Lines);

        var counterpartySelection = ResolveCounterpartySelection(input);
        if (counterpartySelection.IsFailure)
            return counterpartySelection.Error;

        var bankTransaction = await _dbContext.BankTransactions.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankTransaction is null)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        if (bankTransaction.DismissedAt is not null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionDismissed,
                "BankTransaction has been dismissed; undismiss before categorising."
            );
        }

        if (bankTransaction.JournalEntryId is not null)
        {
            return new ConflictError(
                ErrorCodes.BankTransactionAlreadyCategorised,
                "BankTransaction has already been categorised."
            );
        }

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankTransaction.BankAccountId, cancellationToken);
        if (bankAccount is null || bankAccount.AccountId is not { } bankSideAccountId)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransaction must originate from a BankAccount that owns an Account."
            );
        }

        // Wrap the multi-step composite in an explicit transaction so a mid-flow failure
        // (e.g. JE creation rejects an unbalanced split after we've already created a new
        // Counterparty) rolls back the whole thing — no orphan Counterparty rows.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var counterpartyResult = await ResolveCounterpartyAsync(input, cancellationToken);
        if (counterpartyResult.IsFailure)
            return counterpartyResult.Error;
        var counterpartyId = counterpartyResult.Value;

        if (
            !string.IsNullOrWhiteSpace(bankTransaction.CounterpartyAccountNumber)
            && counterpartyId is { } cpId
        )
        {
            var linkResult = await EnsureCounterpartyBankAccountAsync(
                bankTransaction.CounterpartyAccountNumber,
                bankTransaction.CounterpartyName,
                cpId,
                cancellationToken
            );
            if (linkResult.IsFailure)
                return linkResult.Error;
        }

        var lines = new List<CreateJournalLineInput>(input.Lines.Count + 1)
        {
            new CreateJournalLineInput(
                bankSideAccountId,
                bankTransaction.Money.Amount,
                Description: null,
                ReconciliationStatus.Cleared
            ),
        };
        foreach (var line in input.Lines)
        {
            lines.Add(
                new CreateJournalLineInput(
                    line.AccountId,
                    line.Amount,
                    line.Description,
                    ReconciliationStatus.Uncleared
                )
            );
        }

        var entryResult = await _journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: input.Date,
                Description: input.Description,
                CounterpartyId: counterpartyId,
                Lines: lines
            ),
            cancellationToken
        );
        if (entryResult.IsFailure)
            return entryResult.Error;

        // Per ADR 0013, the BT↔JE link lives on the BankTransaction side now. Set the
        // newly-created JE's id on the tracked BT inside the same transaction so a JE
        // creation failure rolls back cleanly, and a successful categorise hides the row
        // from the Inbox via the `b.JournalEntryId IS NULL` filter.
        bankTransaction.JournalEntryId = entryResult.Value!.Id;
        bankTransaction.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Re-read the JE detail so its BankTransactions list reflects the link we just set;
        // the projection inside CreateAsync ran before the BT was wired up.
        var detailResult = await _journalEntryService.GetAsync(
            entryResult.Value.Id,
            cancellationToken
        );
        if (detailResult.IsFailure)
            return detailResult.Error;
        return detailResult.Value!;
    }

    private static Result ResolveCounterpartySelection(CategorizeBankTransactionInput input)
    {
        // A self-transfer (CONTEXT.md / ADR 0014(e)) is a JE with no external party,
        // so both CounterpartyId and NewCounterparty being null is a valid input —
        // only the contradictory "both provided" case is rejected here.
        if (input.CounterpartyId.HasValue && input.NewCounterparty is not null)
        {
            return new InvariantError(
                ErrorCodes.CategoriseCounterpartySelection,
                "Provide at most one of CounterpartyId or NewCounterparty."
            );
        }

        return Result.Success;
    }

    private async Task<Result<CounterpartyId?>> ResolveCounterpartyAsync(
        CategorizeBankTransactionInput input,
        CancellationToken cancellationToken
    )
    {
        if (input.CounterpartyId is { } existingId)
        {
            var exists = await _dbContext
                .Counterparties.Where(c => c.Id == existingId)
                .EnsureExistsAsync("Counterparty", existingId.Value.ToString(), cancellationToken);
            if (exists.IsFailure)
                return exists.Error;
            return new Result<CounterpartyId?>(existingId);
        }

        if (input.NewCounterparty is null)
        {
            return new Result<CounterpartyId?>((CounterpartyId?)null);
        }

        var created = await _counterpartyService.CreateAsync(
            input.NewCounterparty.Name,
            cancellationToken
        );
        if (created.IsFailure)
            return created.Error;
        return new Result<CounterpartyId?>(created.Value!.Id);
    }

    private async Task<Result> EnsureCounterpartyBankAccountAsync(
        string iban,
        string? accountHolderName,
        CounterpartyId counterpartyId,
        CancellationToken cancellationToken
    )
    {
        var existing = await _dbContext.BankAccounts.AnyAsync(
            b => b.Iban == iban,
            cancellationToken
        );
        if (existing)
            return Result.Success;

        var created = await _bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Type: BankAccountType.Current,
                Iban: iban,
                AccountNumber: null,
                CardIdentifier: null,
                Bic: null,
                BankName: null,
                AccountHolderName: accountHolderName,
                CurrencyCode: null,
                ImporterKey: null,
                AccountId: null,
                CounterpartyId: counterpartyId
            ),
            cancellationToken
        );
        if (created.IsFailure)
            return created.Error;

        return Result.Success;
    }
}
