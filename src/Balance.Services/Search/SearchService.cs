using Balance.Data;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Search;

internal sealed class SearchService : ISearchService
{
    /// <summary>
    /// Per-section cap on the launcher result list. Matches the design's
    /// "5 per section + N more matching" affordance.
    /// </summary>
    private const int SectionTake = 5;

    private readonly BalanceDbContext _dbContext;

    public SearchService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SearchOutput> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var needle = query.Trim();
        var likePattern = $"%{needle}%";
        // Card identifiers are stored normalized (uppercase, spaces stripped). Normalize
        // the query the same way so that "1234 5678" matches "1234************5678".
        var cardNeedle = needle
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        var cardLikePattern = $"%{cardNeedle}%";

        var accounts = await SearchAccountsAsync(likePattern, cancellationToken);
        var counterparties = await SearchCounterpartiesAsync(likePattern, cancellationToken);
        var bankAccounts = await SearchBankAccountsAsync(
            likePattern,
            cardLikePattern,
            cancellationToken
        );
        var journalEntries = await SearchJournalEntriesAsync(likePattern, cancellationToken);
        var pages = SearchPages(needle);

        return new SearchOutput(accounts, counterparties, bankAccounts, journalEntries, pages);
    }

    private async Task<SearchSection<AccountHit>> SearchAccountsAsync(
        string likePattern,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext
            .Accounts.AsNoTracking()
            .Where(a => DbFunction.CaseInsensitiveLike(a.Name, likePattern));
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(a => a.Name)
            .Take(SectionTake)
            .Select(a => new AccountHit(a.Id, a.Name, a.AccountType))
            .ToListAsync(cancellationToken);
        return new SearchSection<AccountHit>(items, totalCount);
    }

    private async Task<SearchSection<CounterpartyHit>> SearchCounterpartiesAsync(
        string likePattern,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext
            .Counterparties.AsNoTracking()
            .Where(c => DbFunction.CaseInsensitiveLike(c.Name, likePattern));
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .Take(SectionTake)
            .Select(c => new CounterpartyHit(c.Id, c.Name))
            .ToListAsync(cancellationToken);
        return new SearchSection<CounterpartyHit>(items, totalCount);
    }

    private async Task<SearchSection<BankAccountHit>> SearchBankAccountsAsync(
        string likePattern,
        string cardLikePattern,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b =>
                (b.Iban != null && DbFunction.CaseInsensitiveLike(b.Iban, likePattern))
                || (
                    b.AccountNumber != null
                    && DbFunction.CaseInsensitiveLike(b.AccountNumber, likePattern)
                )
                || (
                    b.CardIdentifier != null
                    && DbFunction.CaseInsensitiveLike(b.CardIdentifier, cardLikePattern)
                )
                || (b.BankName != null && DbFunction.CaseInsensitiveLike(b.BankName, likePattern))
                || (
                    b.AccountHolderName != null
                    && DbFunction.CaseInsensitiveLike(b.AccountHolderName, likePattern)
                )
            );
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.BankName ?? b.AccountHolderName ?? b.Iban ?? b.AccountNumber)
            .Take(SectionTake)
            .Select(b => new BankAccountHit(
                b.Id,
                b.Type,
                b.Iban,
                b.AccountNumber,
                b.CardIdentifier,
                b.BankName,
                b.AccountHolderName
            ))
            .ToListAsync(cancellationToken);
        return new SearchSection<BankAccountHit>(items, totalCount);
    }

    private async Task<SearchSection<JournalEntryHit>> SearchJournalEntriesAsync(
        string likePattern,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e =>
                e.Description != null && DbFunction.CaseInsensitiveLike(e.Description, likePattern)
            );
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Take(SectionTake)
            .Select(e => new JournalEntryHit(e.Id, e.Date, e.Description))
            .ToListAsync(cancellationToken);
        return new SearchSection<JournalEntryHit>(items, totalCount);
    }

    private static SearchSection<PageHit> SearchPages(string needle)
    {
        var all = PageCatalog.Match(needle).ToList();
        var items = all.Take(SectionTake).ToList();
        return new SearchSection<PageHit>(items, all.Count);
    }
}
