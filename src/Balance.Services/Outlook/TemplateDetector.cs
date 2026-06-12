using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Outlook;

/// <summary>
/// Mines the ledger for recurring patterns that have no <c>JournalEntryTemplate</c> yet (ADR-0027
/// hybrid model): groups the trailing P&L-touching activity by the layered matching key, and
/// proposes a candidate where a group recurs on a recognizable rhythm. Pure suggestion — nothing is
/// stored; the user accepts a candidate to create the real (confirmed) template.
/// </summary>
internal static class TemplateDetector
{
    private const int LookbackMonths = 6;
    private const int MinOccurrences = 3;

    public static async Task<IReadOnlyList<TemplateCandidateOutput>> DetectAsync(
        BalanceDbContext dbContext,
        DateOnly today,
        CancellationToken cancellationToken
    )
    {
        var accounts = await dbContext
            .Accounts.AsNoTracking()
            .Where(a =>
                a.IsPostable
                && a.IsLiquid
                && (a.AccountType == AccountType.Asset || a.AccountType == AccountType.Liability)
            )
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.AccountType,
                a.CurrencyCode,
            })
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        if (accounts.Count == 0)
            return [];

        var counterpartyNames = await dbContext
            .Counterparties.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var accountNames = await dbContext
            .Accounts.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        // Keys already covered by a confirmed template — don't re-propose them.
        var existing = await dbContext
            .JournalEntryTemplates.AsNoTracking()
            .Select(t => new
            {
                t.AccountId,
                t.MandateId,
                t.SepaCreditorId,
                t.CounterpartyId,
                t.CounterAccountId,
            })
            .ToListAsync(cancellationToken);
        var covered = existing
            .Select(t =>
                (
                    t.AccountId,
                    Key: OutlookMatching.GroupKey(
                        t.MandateId,
                        t.SepaCreditorId,
                        t.CounterpartyId,
                        t.CounterAccountId
                    )
                )
            )
            .Where(x => x.Key is not null)
            .Select(x => (x.AccountId, Key: x.Key!))
            .ToHashSet();

        var from = today.AddMonths(-LookbackMonths);
        var occurrences = await OutlookLedger.LoadOccurrencesAsync(
            dbContext,
            accounts.Keys,
            from,
            today,
            cancellationToken,
            includeTransfers: true
        );

        var candidates = new List<TemplateCandidateOutput>();
        var groups = occurrences
            .Select(o =>
                (
                    Occurrence: o,
                    Key: OutlookMatching.GroupKey(
                        o.MandateId,
                        o.SepaCreditorId,
                        o.CounterpartyId,
                        o.CounterAccountId
                    )
                )
            )
            .Where(x => x.Key is not null)
            .GroupBy(x => (x.Occurrence.AccountId, Key: x.Key!));

        foreach (var group in groups)
        {
            if (covered.Contains(group.Key))
                continue;

            var items = group.Select(x => x.Occurrence).OrderBy(o => o.Date).ToList();
            var months = items.Select(o => MonthIndex(o.Date)).Distinct().OrderBy(m => m).ToList();
            if (months.Count < MinOccurrences)
                continue;

            var cadence = InferCadence(months);
            if (cadence is null)
                continue;

            var account = accounts[group.Key.AccountId];
            var expectedAmount = Median(items.Select(o => o.Amount).ToList());
            var delta = AccountSignConvention
                .ToBalance(account.AccountType, expectedAmount, account.CurrencyCode)
                .Amount;
            var latest = items[^1];

            candidates.Add(
                new TemplateCandidateOutput(
                    group.Key.AccountId,
                    account.Name,
                    latest.CounterAccountId,
                    latest.CounterAccountId is { } caId
                        ? accountNames.GetValueOrDefault(caId)
                        : null,
                    latest.CounterpartyId,
                    latest.CounterpartyId is { } cpId
                        ? counterpartyNames.GetValueOrDefault(cpId)
                        : null,
                    SuggestName(latest, counterpartyNames, accountNames),
                    cadence.Value,
                    latest.Date,
                    expectedAmount,
                    CadenceMath.MonthlyEquivalent(cadence.Value, delta),
                    items.Count,
                    account.CurrencyCode,
                    latest.MandateId,
                    latest.SepaCreditorId
                )
            );
        }

        // Largest monthly commitment first — the ones worth confirming.
        return [.. candidates.OrderByDescending(c => Math.Abs(c.MonthlyEquivalent))];
    }

    private static Cadence? InferCadence(List<int> sortedMonthIndices)
    {
        if (sortedMonthIndices.Count < 2)
            return null;

        var gaps = new List<int>();
        for (var i = 1; i < sortedMonthIndices.Count; i++)
            gaps.Add(sortedMonthIndices[i] - sortedMonthIndices[i - 1]);

        var medianGap = Median(gaps);
        return medianGap switch
        {
            1 => Cadence.Monthly,
            2 or 3 => Cadence.Quarterly,
            >= 11 and <= 13 => Cadence.Yearly,
            _ => null,
        };
    }

    private static string SuggestName(
        LedgerOccurrence occurrence,
        Dictionary<CounterpartyId, string> counterpartyNames,
        Dictionary<AccountId, string> accountNames
    )
    {
        if (
            occurrence.CounterpartyId is { } cpId
            && counterpartyNames.TryGetValue(cpId, out var cp)
        )
            return cp;
        if (occurrence.CounterAccountId is { } caId && accountNames.TryGetValue(caId, out var ca))
            return ca;
        return "Recurring payment";
    }

    private static int MonthIndex(DateOnly date) => (date.Year * 12) + date.Month;

    private static long Median(List<long> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (long)Math.Round((sorted[mid - 1] + sorted[mid]) / 2m, MidpointRounding.AwayFromZero);
    }

    private static int Median(List<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
