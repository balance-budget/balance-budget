using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Outlook;

internal sealed class OutlookProjectionService : IOutlookService
{
    private const int ActualsMonths = 6;
    private const int TypicalSpendMonths = 3;

    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public OutlookProjectionService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<OutlookProjectionOutput>> GetProjectionAsync(
        CurrencyCode currencyCode,
        int horizonMonths,
        OutlookScenarioInput? scenario,
        CancellationToken cancellationToken
    )
    {
        var horizon = Math.Clamp(horizonMonths, 1, 120);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var anchorMonth = FirstOfMonth(today);
        var fromMonth = anchorMonth.AddMonths(1);

        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a =>
                a.IsPostable
                && a.IsLiquid
                && a.CurrencyCode == currencyCode
                && (a.AccountType == AccountType.Asset || a.AccountType == AccountType.Liability)
            )
            .Select(a => new LiquidAccount(
                a.Id,
                a.Name,
                a.AccountType,
                a.CurrencyCode,
                _dbContext.JournalLines.Where(l => l.AccountId == a.Id).Sum(l => (long?)l.Amount)
                    ?? 0L
            ))
            .ToListAsync(cancellationToken);
        if (accounts.Count == 0)
            return new OutlookProjectionOutput(anchorMonth, horizon, []);

        var accountIds = accounts.Select(a => a.Id).ToList();

        var templates = await _dbContext
            .JournalEntryTemplates.AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId))
            .ToListAsync(cancellationToken);
        var templatesByAccount = templates
            .GroupBy(t => t.AccountId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<JournalEntryTemplate>)[.. g]);

        var typicalSpend = await ComputeTypicalSpendAsync(
            accounts,
            templatesByAccount,
            anchorMonth,
            cancellationToken
        );
        var actuals = await LoadActualsAsync(accounts, anchorMonth, cancellationToken);

        var output = new List<OutlookAccountProjectionOutput>(accounts.Count);
        foreach (var account in accounts)
        {
            var currentBalance = ToBalance(account, account.CurrentBalanceRaw);
            var accountTemplates = templatesByAccount.GetValueOrDefault(account.Id, []);
            var band = typicalSpend.GetValueOrDefault(account.Id, OutlookSpendBand.Zero);

            var baselineSpecs = accountTemplates
                .Select(t => ToSpec(account, t.Cadence, t.AnchorDate, t.EndDate, t.ExpectedAmount))
                .ToList();
            var baseline = OutlookProjectionEngine.Project(
                currentBalance,
                baselineSpecs,
                band,
                fromMonth,
                horizon
            );

            IReadOnlyList<OutlookProjectedMonthOutput>? scenarioRows = null;
            if (scenario is not null)
            {
                var scenarioSpecs = BuildScenarioSpecs(account, accountTemplates, scenario);
                scenarioRows =
                [
                    .. OutlookProjectionEngine
                        .Project(currentBalance, scenarioSpecs, band, fromMonth, horizon)
                        .Select(ToMonthOutput),
                ];
            }

            output.Add(
                new OutlookAccountProjectionOutput(
                    account.Id,
                    account.Name,
                    account.AccountType,
                    account.CurrencyCode,
                    currentBalance,
                    actuals.GetValueOrDefault(account.Id, []),
                    [.. baseline.Select(ToMonthOutput)],
                    scenarioRows
                )
            );
        }

        return new OutlookProjectionOutput(anchorMonth, horizon, output);
    }

    // ---- Typical spend --------------------------------------------------------------------------

    private async Task<IReadOnlyDictionary<AccountId, OutlookSpendBand>> ComputeTypicalSpendAsync(
        IReadOnlyList<LiquidAccount> accounts,
        IReadOnlyDictionary<AccountId, IReadOnlyList<JournalEntryTemplate>> templatesByAccount,
        DateOnly anchorMonth,
        CancellationToken cancellationToken
    )
    {
        // The three whole months before the current one.
        var windowStart = anchorMonth.AddMonths(-TypicalSpendMonths);
        var windowEnd = anchorMonth.AddDays(-1);
        var monthStarts = Enumerable
            .Range(0, TypicalSpendMonths)
            .Select(windowStart.AddMonths)
            .ToList();

        var occurrences = await OutlookLedger.LoadOccurrencesAsync(
            _dbContext,
            accounts.Select(a => a.Id).ToList(),
            windowStart,
            windowEnd,
            cancellationToken
        );
        var byAccount = occurrences
            .GroupBy(o => o.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<AccountId, OutlookSpendBand>(accounts.Count);
        foreach (var account in accounts)
        {
            if (!byAccount.TryGetValue(account.Id, out var accountOccurrences))
                continue;

            var accountTemplates = templatesByAccount.GetValueOrDefault(account.Id, []);
            var monthlyNets = monthStarts
                .Select(monthStart =>
                {
                    var monthEnd = LastOfMonth(monthStart);
                    var raw = accountOccurrences
                        .Where(o => o.Date >= monthStart && o.Date <= monthEnd)
                        .Where(o => !MatchesAnyTemplate(o, accountTemplates))
                        .Sum(o => o.Amount);
                    return ToBalance(account, raw);
                })
                .OrderBy(net => net)
                .ToList();

            // Low = pessimistic (most negative), High = optimistic, Mid = median across the months.
            result[account.Id] = new OutlookSpendBand(
                monthlyNets[0],
                Median(monthlyNets),
                monthlyNets[^1]
            );
        }

        return result;
    }

    private static bool MatchesAnyTemplate(
        LedgerOccurrence occurrence,
        IReadOnlyList<JournalEntryTemplate> templates
    )
    {
        var occurrenceKey = OutlookMatching.GroupKey(
            occurrence.MandateId,
            occurrence.SepaCreditorId,
            occurrence.CounterpartyId,
            occurrence.CounterAccountId
        );
        if (occurrenceKey is null)
            return false;

        foreach (var template in templates)
        {
            var templateKey = OutlookMatching.GroupKey(
                template.MandateId,
                template.SepaCreditorId,
                template.CounterpartyId,
                template.CounterAccountId
            );
            if (
                templateKey == occurrenceKey
                && OutlookMatching.AmountWithinBand(occurrence.Amount, template.ExpectedAmount)
            )
            {
                return true;
            }
        }

        return false;
    }

    // ---- Actuals --------------------------------------------------------------------------------

    private async Task<
        IReadOnlyDictionary<AccountId, IReadOnlyList<OutlookActualPointOutput>>
    > LoadActualsAsync(
        IReadOnlyList<LiquidAccount> accounts,
        DateOnly anchorMonth,
        CancellationToken cancellationToken
    )
    {
        var accountIds = accounts.Select(a => a.Id).ToList();
        var windowStart = anchorMonth.AddMonths(-(ActualsMonths - 1));

        // One batched query: per (account, month) net raw movement within the actuals window.
        var deltaRows = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => accountIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking().Where(e => e.Date >= windowStart),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.AccountId,
                        e.Date,
                        l.Amount,
                    }
            )
            .GroupBy(x => new
            {
                x.AccountId,
                x.Date.Year,
                x.Date.Month,
            })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Year,
                g.Key.Month,
                Sum = g.Sum(x => (long?)x.Amount) ?? 0L,
            })
            .ToListAsync(cancellationToken);

        var deltasByAccount = deltaRows
            .GroupBy(r => r.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => new DateOnly(r.Year, r.Month, 1), r => r.Sum)
            );

        var result = new Dictionary<AccountId, IReadOnlyList<OutlookActualPointOutput>>(
            accounts.Count
        );
        foreach (var account in accounts)
        {
            var deltas = deltasByAccount.GetValueOrDefault(account.Id, []);

            // Walk backward from the current balance, peeling off each month's movement, so the
            // last point lands exactly on today's balance and the projection picks up from there.
            var points = new List<OutlookActualPointOutput>(ActualsMonths);
            var balance = ToBalance(account, account.CurrentBalanceRaw);
            for (var month = anchorMonth; month >= windowStart; month = month.AddMonths(-1))
            {
                points.Add(new OutlookActualPointOutput(month, balance));
                deltas.TryGetValue(month, out var rawDelta);
                balance -= ToBalance(account, rawDelta);
            }

            points.Reverse();
            result[account.Id] = points;
        }

        return result;
    }

    // ---- Scenario -------------------------------------------------------------------------------

    private static List<OutlookTemplateSpec> BuildScenarioSpecs(
        LiquidAccount account,
        IReadOnlyList<JournalEntryTemplate> accountTemplates,
        OutlookScenarioInput scenario
    )
    {
        var disabled = scenario.DisabledTemplateIds.ToHashSet();
        var overrides = scenario.AmountOverrides.ToDictionary(
            o => o.TemplateId,
            o => o.ExpectedAmount
        );

        var specs = new List<OutlookTemplateSpec>();
        foreach (var template in accountTemplates)
        {
            if (disabled.Contains(template.Id))
                continue;

            var amount = overrides.GetValueOrDefault(template.Id, template.ExpectedAmount);
            specs.Add(
                ToSpec(account, template.Cadence, template.AnchorDate, template.EndDate, amount)
            );
        }

        foreach (var added in scenario.AddedTemplates.Where(a => a.AccountId == account.Id))
            specs.Add(
                ToSpec(
                    account,
                    added.Cadence,
                    added.AnchorDate,
                    added.EndDate,
                    added.ExpectedAmount
                )
            );

        return specs;
    }

    // ---- helpers --------------------------------------------------------------------------------

    private static OutlookTemplateSpec ToSpec(
        LiquidAccount account,
        Cadence cadence,
        DateOnly anchorDate,
        DateOnly? endDate,
        long rawExpectedAmount
    ) => new(cadence, anchorDate, endDate, ToBalance(account, rawExpectedAmount));

    private static OutlookProjectedMonthOutput ToMonthOutput(OutlookMonthRow row) =>
        new(row.Month, row.ExpectedNet, row.SpendMid, row.EndLow, row.EndMid, row.EndHigh);

    private static long ToBalance(LiquidAccount account, long raw) =>
        AccountSignConvention.ToBalance(account.AccountType, raw, account.CurrencyCode).Amount;

    private static long Median(List<long> sortedValues)
    {
        var mid = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 1
            ? sortedValues[mid]
            : (long)
                Math.Round(
                    (sortedValues[mid - 1] + sortedValues[mid]) / 2m,
                    MidpointRounding.AwayFromZero
                );
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly LastOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private sealed record LiquidAccount(
        AccountId Id,
        string Name,
        AccountType AccountType,
        CurrencyCode CurrencyCode,
        long CurrentBalanceRaw
    );
}
