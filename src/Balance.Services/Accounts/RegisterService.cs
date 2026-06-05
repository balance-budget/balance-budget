using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Accounts;

internal sealed class RegisterService : IRegisterService
{
    private readonly BalanceDbContext _dbContext;

    public RegisterService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedOutput<RegisterRowOutput>>> ListAsync(
        AccountId accountId,
        int skip,
        int take,
        RegisterListFilter filter,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(filter);

        var account = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.AccountType, a.CurrencyCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return new NotFoundError("Account", accountId.Value.ToString());
        }

        // The focal set is this account plus every descendant (ADR-0019): a leaf register shows its
        // own lines; a non-postable account's register aggregates all descendant leaves' lines,
        // merged newest-first with no intra-subtree elimination.
        var nodes = await _dbContext
            .Accounts.AsNoTracking()
            .Select(a => new AccountNode(a.Id, a.ParentAccountId))
            .ToListAsync(cancellationToken);
        var focalIds = AccountTree.DescendantsAndSelf(nodes, accountId).ToList();

        // The posted-account filter narrows the focal set to one descendant's subtree —
        // an account outside the viewed subtree simply intersects to nothing.
        if (filter.PostedAccountId is { } postedId)
        {
            if (!nodes.Any(n => n.Id == postedId))
            {
                return new NotFoundError("Account", postedId.Value.ToString());
            }

            var postedIds = AccountTree.DescendantsAndSelf(nodes, postedId);
            focalIds = focalIds.Where(postedIds.Contains).ToList();
        }

        // Focal lines on the subtree, joined to their entry so the optional `?q=`
        // filter (and ordering) can read the entry header. The same query backs both
        // the count and the page so they never disagree.
        var lines = _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => focalIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) => new { Line = l, Entry = e }
            );

        // The counter-account filter keeps rows whose entry has at least one OTHER line on the
        // chosen account's subtree — "other" by line id, mirroring how the counter legs are
        // derived below, so an intra-subtree transfer still matches its sibling leg.
        if (filter.CounterAccountId is { } counterId)
        {
            if (!nodes.Any(n => n.Id == counterId))
            {
                return new NotFoundError("Account", counterId.Value.ToString());
            }

            var counterIds = AccountTree.DescendantsAndSelf(nodes, counterId).ToList();
            lines = lines.Where(x =>
                _dbContext.JournalLines.Any(l2 =>
                    l2.JournalEntryId == x.Entry.Id
                    && l2.Id != x.Line.Id
                    && counterIds.Contains(l2.AccountId)
                )
            );
        }

        if (filter.From is { } from)
        {
            lines = lines.Where(x => x.Entry.Date >= from);
        }

        if (filter.To is { } to)
        {
            lines = lines.Where(x => x.Entry.Date <= to);
        }

        if (filter.Status is { } status)
        {
            lines = lines.Where(x => x.Line.ReconciliationStatus == status);
        }

        var needle = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            // Match the entry Description or its linked Counterparty's Name, sharing the exact
            // predicate with the journal-entries list filter via JournalEntryFilters so the two
            // can't drift (ADR-0017 item (g) amendment). Applied as an entry-id subquery because
            // `lines` is the line↔entry join rather than a bare JournalEntry query.
            var matchingEntryIds = _dbContext
                .JournalEntries.AsNoTracking()
                .Where(JournalEntryFilters.MatchesNeedle(_dbContext, needle))
                .Select(e => e.Id);
            lines = lines.Where(x => matchingEntryIds.Contains(x.Entry.Id));
        }

        var totalCount = await lines.CountAsync(cancellationToken);

        if (take <= 0)
        {
            return new Result<PagedOutput<RegisterRowOutput>>(
                new PagedOutput<RegisterRowOutput>([], totalCount)
            );
        }

        // Page focal lines, ordered Date DESC then JournalEntryId DESC per ADR-0007.
        // The CounterpartyName is fetched via a correlated subquery so one round-trip
        // captures everything needed for the page header columns.
        var focalRows = await lines
            .OrderByDescending(x => x.Entry.Date)
            .ThenByDescending(x => x.Entry.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new FocalRow(
                x.Line.Id,
                x.Entry.Id,
                x.Entry.Date,
                x.Entry.Description,
                x.Entry.CounterpartyId,
                x.Entry.CounterpartyId == null
                    ? null
                    : _dbContext
                        .Counterparties.Where(c => c.Id == x.Entry.CounterpartyId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                x.Line.Amount,
                x.Line.ReconciliationStatus,
                x.Line.Description
            ))
            .ToListAsync(cancellationToken);

        if (focalRows.Count == 0)
        {
            return new Result<PagedOutput<RegisterRowOutput>>(
                new PagedOutput<RegisterRowOutput>([], totalCount)
            );
        }

        var entryIds = focalRows.Select(r => r.EntryId).Distinct().ToList();

        // All lines for the focal entries, joined to Accounts for each offsetting leg's name and
        // currency (each leg renders in its own account's currency). The counter legs of a focal row
        // are every OTHER line in its entry, excluded by line id rather than account id — so an
        // intra-subtree transfer shows both legs, each as its own focal row with the sibling as its
        // counter (no elimination, ADR-0019).
        var entryLines = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => entryIds.Contains(l.JournalEntryId))
            .Join(
                _dbContext.Accounts.AsNoTracking(),
                l => l.AccountId,
                a => a.Id,
                (l, a) =>
                    new SiblingRow(l.JournalEntryId, l.Id, a.Id, a.Name, l.Amount, a.CurrencyCode)
            )
            .ToListAsync(cancellationToken);

        var linesByEntry = entryLines
            .GroupBy(s => s.EntryId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SiblingRow>)g.ToList());

        // The focal line itself is part of `entryLines`, so the posted account (the descendant
        // leaf the line actually sits on — equal to the viewed account for a leaf register)
        // comes from the same fetch.
        var lineById = entryLines.ToDictionary(s => s.LineId);

        var output = new List<RegisterRowOutput>(focalRows.Count);
        foreach (var row in focalRows)
        {
            var posted = lineById[row.LineId];
            var focalAmount = AccountSignConvention.ToBalance(
                account.AccountType,
                row.Amount,
                account.CurrencyCode
            );

            IReadOnlyList<RegisterRowCounterLeg> counter;
            if (linesByEntry.TryGetValue(row.EntryId, out var sibs))
            {
                var legs = new List<RegisterRowCounterLeg>(sibs.Count);
                foreach (var sib in sibs.Where(s => s.LineId != row.LineId).OrderBy(s => s.LineId))
                {
                    legs.Add(
                        new RegisterRowCounterLeg(
                            sib.AccountId,
                            sib.AccountName,
                            new Money(sib.Amount, sib.CurrencyCode)
                        )
                    );
                }

                counter = legs;
            }
            else
            {
                counter = [];
            }

            output.Add(
                new RegisterRowOutput(
                    row.EntryId,
                    row.LineId,
                    posted.AccountId,
                    posted.AccountName,
                    row.Date,
                    row.EntryDescription,
                    row.CounterpartyId,
                    row.CounterpartyName,
                    row.LineDescription,
                    row.ReconciliationStatus,
                    focalAmount,
                    counter
                )
            );
        }

        return new Result<PagedOutput<RegisterRowOutput>>(
            new PagedOutput<RegisterRowOutput>(output, totalCount)
        );
    }

    private sealed record FocalRow(
        JournalLineId LineId,
        JournalEntryId EntryId,
        DateOnly Date,
        string? EntryDescription,
        CounterpartyId? CounterpartyId,
        string? CounterpartyName,
        long Amount,
        ReconciliationStatus ReconciliationStatus,
        string? LineDescription
    );

    private sealed record SiblingRow(
        JournalEntryId EntryId,
        JournalLineId LineId,
        AccountId AccountId,
        string AccountName,
        long Amount,
        CurrencyCode CurrencyCode
    );
}
