using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Counterparties;

internal sealed class AccountSuggestionService : IAccountSuggestionService
{
    private readonly BalanceDbContext _dbContext;

    public AccountSuggestionService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<
        Result<IReadOnlyList<SuggestedCounterAccountOutput>>
    > GetSuggestedCounterAccountsAsync(
        CounterpartyId counterpartyId,
        CancellationToken cancellationToken
    )
    {
        var exists = await _dbContext.Counterparties.AnyAsync(
            c => c.Id == counterpartyId,
            cancellationToken
        );
        if (!exists)
        {
            return new NotFoundError("Counterparty", counterpartyId.Value.ToString());
        }

        // Most recent JournalEntry for this counterparty (Date desc, CreatedAt desc per ADR-0007).
        var mostRecentEntryId = await _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e => e.CounterpartyId == counterpartyId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => (JournalEntryId?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecentEntryId is null)
        {
            return new Result<IReadOnlyList<SuggestedCounterAccountOutput>>([]);
        }

        // Project counter-side lines. We exclude lines whose Account is `Cleared` on the most
        // recent JE — that line was the bank-side line auto-created by the categorization
        // flow, and re-suggesting it would round-trip noise back into the form. We do NOT
        // exclude every Account that happens to have a BankAccount linked, because a counter-
        // side line can legitimately reference an own-Account (e.g. paying down a Liability
        // **Account** with a Card **BankAccount** linked, or Current → Savings); blanket-
        // excluding those would hide the very accounts the flow most wants to suggest for
        // self-transfers.
        var suggestions = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l =>
                l.JournalEntryId == mostRecentEntryId
                && l.ReconciliationStatus != ReconciliationStatus.Cleared
            )
            .OrderBy(l => l.Id)
            .Select(l => new SuggestedCounterAccountOutput(l.AccountId, l.Amount))
            .ToListAsync(cancellationToken);

        return new Result<IReadOnlyList<SuggestedCounterAccountOutput>>(suggestions);
    }
}
