using Balance.Data;
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

        // Most recent JournalEntry for this counterparty (Date desc, CreatedAt desc per ADR-0008).
        var mostRecentEntryId = await _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e => e.CounterpartyId == counterpartyId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => (JournalEntryId?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecentEntryId is null)
        {
            return new Result<IReadOnlyList<SuggestedCounterAccountOutput>>(
                Array.Empty<SuggestedCounterAccountOutput>()
            );
        }

        // Project non-bank-side lines: an Account is "bank-side" iff it is the owning Account
        // of some BankAccount (BankAccount.AccountId IS NOT NULL pointing at it). The
        // categorisation flow always creates one such bank-side line plus N counter-side
        // lines, so excluding bank-side reproduces the counter-side shape we want to suggest.
        var suggestions = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l =>
                l.JournalEntryId == mostRecentEntryId
                && !_dbContext.BankAccounts.Any(ba => ba.AccountId == l.AccountId)
            )
            .OrderBy(l => l.Id)
            .Select(l => new SuggestedCounterAccountOutput(l.AccountId, l.Amount))
            .ToListAsync(cancellationToken);

        return new Result<IReadOnlyList<SuggestedCounterAccountOutput>>(suggestions);
    }
}
