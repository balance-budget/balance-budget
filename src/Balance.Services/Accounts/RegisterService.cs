using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
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
        string? search,
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

        // Focal lines on this account, joined to their entry so the optional `?q=`
        // filter (and ordering) can read the entry header. The same query backs both
        // the count and the page so they never disagree.
        var lines = _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) => new { Line = l, Entry = e }
            );

        var needle = search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            // Match the entry Description or its linked Counterparty's Name, mirroring the
            // journal-entries list filter (see the ADR-0020 item (g) amendment).
            var pattern = $"%{needle}%";
            lines = lines.Where(x =>
                (
                    x.Entry.Description != null
                    && DbFunction.CaseInsensitiveLike(x.Entry.Description, pattern)
                )
                || (
                    x.Entry.CounterpartyId != null
                    && _dbContext.Counterparties.Any(c =>
                        c.Id == x.Entry.CounterpartyId
                        && DbFunction.CaseInsensitiveLike(c.Name, pattern)
                    )
                )
            );
        }

        var totalCount = await lines.CountAsync(cancellationToken);

        if (take <= 0)
        {
            return new Result<PagedOutput<RegisterRowOutput>>(
                new PagedOutput<RegisterRowOutput>(Array.Empty<RegisterRowOutput>(), totalCount)
            );
        }

        var isCreditNormal = AccountSignConvention.IsCreditNormal(account.AccountType);

        // Page focal lines, ordered Date DESC then JournalEntryId DESC per ADR-0008.
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
                new PagedOutput<RegisterRowOutput>(Array.Empty<RegisterRowOutput>(), totalCount)
            );
        }

        var entryIds = focalRows.Select(r => r.EntryId).Distinct().ToList();

        // Sibling lines for the same entries — every line whose AccountId is not the
        // focal account. Joined to Accounts for the offsetting account's name and
        // currency (each leg renders in its own account's currency).
        var siblings = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => entryIds.Contains(l.JournalEntryId) && l.AccountId != accountId)
            .Join(
                _dbContext.Accounts.AsNoTracking(),
                l => l.AccountId,
                a => a.Id,
                (l, a) =>
                    new SiblingRow(l.JournalEntryId, l.Id, a.Id, a.Name, l.Amount, a.CurrencyCode)
            )
            .ToListAsync(cancellationToken);

        var siblingsByEntry = siblings
            .GroupBy(s => s.EntryId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SiblingRow>)g.ToList());

        var output = new List<RegisterRowOutput>(focalRows.Count);
        foreach (var row in focalRows)
        {
            var focalAmount = new Money(
                isCreditNormal ? checked(-row.Amount) : row.Amount,
                account.CurrencyCode
            );

            IReadOnlyList<RegisterRowCounterLeg> counter;
            if (siblingsByEntry.TryGetValue(row.EntryId, out var sibs))
            {
                var legs = new List<RegisterRowCounterLeg>(sibs.Count);
                foreach (var sib in sibs.OrderBy(s => s.LineId))
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
                counter = Array.Empty<RegisterRowCounterLeg>();
            }

            output.Add(
                new RegisterRowOutput(
                    row.EntryId,
                    row.LineId,
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
