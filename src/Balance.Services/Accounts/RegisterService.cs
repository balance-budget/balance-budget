using System.Diagnostics;
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

    public async Task<Result<RegisterSummaryOutput>> SummarizeAsync(
        AccountId accountId,
        DateOnly fromDate,
        DateOnly toDate,
        RegisterSummaryBucket bucket,
        CancellationToken cancellationToken
    )
    {
        var account = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.AccountType, a.CurrencyCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return new NotFoundError("Account", accountId.Value.ToString());
        }

        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.ParentAccountId,
                a.Name,
                a.Code,
            })
            .ToListAsync(cancellationToken);
        var nodes = accounts.Select(a => new AccountNode(a.Id, a.ParentAccountId)).ToList();

        // Each posted line attributes to the direct child of the focal account that its posted
        // account sits under — deeper descendants roll up into their direct-child ancestor. Lines
        // posted on the focal account itself (the leaf case) segment as the account itself.
        var segmentByAccount = new Dictionary<AccountId, AccountId> { [accountId] = accountId };
        foreach (var child in accounts.Where(a => a.ParentAccountId == accountId))
        {
            foreach (var id in AccountTree.DescendantsAndSelf(nodes, child.Id))
            {
                segmentByAccount[id] = child.Id;
            }
        }

        var focalIds = segmentByAccount.Keys.ToList();

        // Pre-aggregate per (entry date, posted account) in the database; bucket boundaries are
        // calendar logic the providers don't share, so the bucket fold happens in memory on the
        // already-collapsed rows.
        var dailySums = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => focalIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        e.Date,
                        l.AccountId,
                        l.Amount,
                    }
            )
            .Where(x => x.Date >= fromDate && x.Date <= toDate)
            .GroupBy(x => new { x.Date, x.AccountId })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.AccountId,
                Sum = g.Sum(x => x.Amount),
            })
            .ToListAsync(cancellationToken);

        // Net per (bucket, segment), normalized to the account's normal balance (ADR-0011) — the
        // whole subtree shares the focal AccountType (ADR-0019), so one flip covers every segment.
        var creditNormal = AccountSignConvention.IsCreditNormal(account.AccountType);
        var sums = new Dictionary<(DateOnly Start, AccountId Segment), long>();
        foreach (var row in dailySums)
        {
            var key = (BucketStart(row.Date, bucket), segmentByAccount[row.AccountId]);
            var amount = creditNormal ? checked(-row.Sum) : row.Sum;
            sums[key] = checked(sums.GetValueOrDefault(key) + amount);
        }

        var activeSegments = sums.Keys.Select(k => k.Segment).ToHashSet();
        var segments = accounts
            .Where(a => activeSegments.Contains(a.Id))
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => new RegisterSummarySegment(a.Id, a.Name))
            .ToList();

        var buckets = new List<RegisterSummaryBucketOutput>();
        for (
            var start = BucketStart(fromDate, bucket);
            start <= toDate;
            start = NextBucket(start, bucket)
        )
        {
            var values = segments
                .Where(s => sums.ContainsKey((start, s.AccountId)))
                .Select(s => new RegisterSummaryValue(s.AccountId, sums[(start, s.AccountId)]))
                .ToList();
            buckets.Add(new RegisterSummaryBucketOutput(start, values));
        }

        return new Result<RegisterSummaryOutput>(
            new RegisterSummaryOutput(
                bucket,
                fromDate,
                toDate,
                account.CurrencyCode,
                segments,
                buckets
            )
        );
    }

    private static DateOnly BucketStart(DateOnly date, RegisterSummaryBucket bucket) =>
        bucket switch
        {
            RegisterSummaryBucket.Day => date,
            // ISO weeks: Monday starts the week.
            RegisterSummaryBucket.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            RegisterSummaryBucket.Month => new DateOnly(date.Year, date.Month, 1),
            _ => throw new UnreachableException($"Unknown RegisterSummaryBucket '{bucket}'."),
        };

    private static DateOnly NextBucket(DateOnly start, RegisterSummaryBucket bucket) =>
        bucket switch
        {
            RegisterSummaryBucket.Day => start.AddDays(1),
            RegisterSummaryBucket.Week => start.AddDays(7),
            RegisterSummaryBucket.Month => start.AddMonths(1),
            _ => throw new UnreachableException($"Unknown RegisterSummaryBucket '{bucket}'."),
        };

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
