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
    private const int TypicalSpendMonths = 6;

    // Consistency factor turning a median-absolute-deviation into a robust standard-deviation
    // estimate for roughly normal data (σ ≈ 1.4826 · MAD), so the ±1-sigma cone of ADR-0033 is a
    // genuine sigma rather than a raw MAD.
    private const double MadToSigma = 1.4826;

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

        // Project from the current (partial) month; always reach December so the year-end card has a
        // figure even when the horizon toggle is shorter than the rest of the year (ADR-0028).
        var monthsToYearEnd = 12 - today.Month + 1;
        var projectMonths = Math.Max(horizon, monthsToYearEnd);

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

        var counterpartyNames = await LoadCounterpartyNamesAsync(templates, cancellationToken);

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
            var band = typicalSpend.GetValueOrDefault(account.Id, OutlookSpendModel.Zero);

            var baselineSpecs = accountTemplates
                .Select(t => ToSpec(account, t.Cadence, t.AnchorDate, t.EndDate, t.ExpectedAmount))
                .ToList();
            var rows = OutlookProjectionEngine.Project(
                currentBalance,
                baselineSpecs,
                band,
                today,
                projectMonths
            );

            IReadOnlyList<OutlookProjectedMonthOutput>? scenarioRows = null;
            if (scenario is not null)
            {
                var scenarioSpecs = BuildScenarioSpecs(account, accountTemplates, scenario);
                scenarioRows =
                [
                    .. OutlookProjectionEngine
                        .Project(currentBalance, scenarioSpecs, band, today, projectMonths)
                        .Take(horizon)
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
                    ToThisMonth(
                        rows[0],
                        BuildExpectedItems(account, accountTemplates, today, counterpartyNames)
                    ),
                    ToYearEnd(rows[monthsToYearEnd - 1], today.Year),
                    actuals.GetValueOrDefault(account.Id, []),
                    [.. rows.Take(horizon).Select(ToMonthOutput)],
                    scenarioRows
                )
            );
        }

        return new OutlookProjectionOutput(anchorMonth, horizon, output);
    }

    // ---- Typical spend --------------------------------------------------------------------------

    private async Task<IReadOnlyDictionary<AccountId, OutlookSpendModel>> ComputeTypicalSpendAsync(
        IReadOnlyList<LiquidAccount> accounts,
        IReadOnlyDictionary<AccountId, IReadOnlyList<JournalEntryTemplate>> templatesByAccount,
        DateOnly anchorMonth,
        CancellationToken cancellationToken
    )
    {
        // The whole months before the current one (ADR-0033: a six-month window).
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
            // Typical spend is one-sided everyday *spend* (ADR-0033): only Expense-leg movement.
            // Non-recurring income (windfalls) and self-transfers never count.
            .Where(o => o.PnlAccountType == AccountType.Expense)
            .GroupBy(o => o.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<AccountId, OutlookSpendModel>(accounts.Count);
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
                .ToList();

            // Drop the leading empty months before this account had any everyday spend, so a
            // recently-opened account isn't dragged toward zero by months it did not yet exist for.
            var firstActive = monthlyNets.FindIndex(net => net != 0L);
            if (firstActive < 0)
                continue;
            var sample = monthlyNets.Skip(firstActive).ToList();

            var sorted = sample.OrderBy(net => net).ToList();
            var median = Median(sorted);

            // Robust spread (ADR-0033): a scaled median-absolute-deviation, so a single one-off
            // month barely moves it. With too thin a history to estimate dispersion, fall back to
            // the median's own magnitude so the cone is honestly wide rather than fake-tight.
            long spread;
            if (sample.Count < 2)
            {
                spread = Math.Abs(median);
            }
            else
            {
                var deviations = sorted
                    .Select(net => Math.Abs(net - median))
                    .OrderBy(d => d)
                    .ToList();
                var mad = Median(deviations);
                spread = (long)Math.Round(mad * MadToSigma, MidpointRounding.AwayFromZero);
            }

            result[account.Id] = new OutlookSpendModel(median, spread);
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

        // Actuals are completed months ending at the previous month; the current (partial) month is
        // owned by the projection now (ADR-0028). Reach back one extra month so the current month's
        // movement can be peeled off the live balance to land on the previous month-end.
        var lastActual = anchorMonth.AddMonths(-1);
        var windowStart = anchorMonth.AddMonths(-ActualsMonths);

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

            // Peel the current (partial) month off the live balance to reach the previous month-end,
            // then walk backward, peeling each completed month, so actuals end where the projection
            // begins and the two curves meet.
            var balance = ToBalance(account, account.CurrentBalanceRaw);
            deltas.TryGetValue(anchorMonth, out var currentMonthDelta);
            balance -= ToBalance(account, currentMonthDelta);

            var points = new List<OutlookActualPointOutput>(ActualsMonths);
            for (var month = lastActual; month >= windowStart; month = month.AddMonths(-1))
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
        new(
            row.Month,
            row.ExpectedIn,
            row.ExpectedOut,
            row.ExpectedNet,
            row.SpendMid,
            row.EndLow,
            row.EndMid,
            row.EndHigh
        );

    private static OutlookThisMonthOutput ToThisMonth(
        OutlookMonthRow row,
        IReadOnlyList<OutlookExpectedItemOutput> items
    ) =>
        new(
            row.Month,
            row.ExpectedIn,
            row.ExpectedOut,
            items,
            row.SpendLow,
            row.SpendHigh,
            row.EndLow,
            row.EndMid,
            row.EndHigh
        );

    /// <summary>
    /// The recurring items still due before month-end, one line per template that has a remaining
    /// occurrence — amount is its per-occurrence delta times the remaining count, due date the
    /// earliest still ahead. Ordered by due date.
    /// </summary>
    private static IReadOnlyList<OutlookExpectedItemOutput> BuildExpectedItems(
        LiquidAccount account,
        IReadOnlyList<JournalEntryTemplate> templates,
        DateOnly today,
        IReadOnlyDictionary<CounterpartyId, string> counterpartyNames
    )
    {
        var items = new List<OutlookExpectedItemOutput>();
        foreach (var template in templates)
        {
            var spec = ToSpec(
                account,
                template.Cadence,
                template.AnchorDate,
                template.EndDate,
                template.ExpectedAmount
            );
            var dates = OutlookProjectionEngine.RemainingOccurrences(spec, today);
            if (dates.Count == 0)
                continue;

            items.Add(
                new OutlookExpectedItemOutput(
                    template.Name,
                    template.CounterpartyId,
                    template.CounterpartyId is { } cp
                        ? counterpartyNames.GetValueOrDefault(cp)
                        : null,
                    spec.Delta * dates.Count,
                    dates.Min()
                )
            );
        }

        return [.. items.OrderBy(i => i.DueDate)];
    }

    private async Task<IReadOnlyDictionary<CounterpartyId, string>> LoadCounterpartyNamesAsync(
        IReadOnlyList<JournalEntryTemplate> templates,
        CancellationToken cancellationToken
    )
    {
        var ids = templates
            .Where(t => t.CounterpartyId is not null)
            .Select(t => t.CounterpartyId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<CounterpartyId, string>();

        return await _dbContext
            .Counterparties.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    private static OutlookYearEndOutput ToYearEnd(OutlookMonthRow row, int year) =>
        new(new DateOnly(year, 12, 31), row.EndLow, row.EndMid, row.EndHigh);

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
