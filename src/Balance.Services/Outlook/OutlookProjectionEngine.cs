using Balance.Data.Entities.Enums;

namespace Balance.Services.Outlook;

/// <summary>
/// The pure forward half of the liquid-balance <c>Projection</c> (ADR-0027): from an opening
/// balance, a set of <see cref="JournalEntryTemplate"/> specs, and a <c>Typical spend</c> band,
/// it produces one month-end balance band per future month. Computed, never stored; the past
/// (ledger actuals) is supplied separately by the service and meets this curve at the anchor.
/// All amounts are minor units, <em>balance-normalized</em> to the account's own perspective
/// (inflow positive, outflow negative) — the service applies the Sign convention before calling in,
/// so the engine never needs to know an account's debit/credit normality.
/// </summary>
internal static class OutlookProjectionEngine
{
    /// <summary>
    /// Projects <paramref name="horizonMonths"/> whole months starting at <paramref name="fromMonth"/>
    /// (normalized to the first of the month). Each month accumulates the expected template
    /// occurrences landing in it plus the <c>Typical spend</c> band onto the running balance.
    /// </summary>
    public static IReadOnlyList<OutlookMonthRow> Project(
        long openingBalance,
        IReadOnlyList<OutlookTemplateSpec> templates,
        OutlookSpendBand typicalSpend,
        DateOnly fromMonth,
        int horizonMonths
    )
    {
        ArgumentNullException.ThrowIfNull(templates);

        var start = FirstOfMonth(fromMonth);
        var rows = new List<OutlookMonthRow>(horizonMonths);

        var balanceLow = openingBalance;
        var balanceMid = openingBalance;
        var balanceHigh = openingBalance;

        for (var i = 0; i < horizonMonths; i++)
        {
            var month = start.AddMonths(i);
            var monthEnd = LastOfMonth(month);

            var expectedNet = 0L;
            foreach (var template in templates)
                expectedNet += template.Delta * OccurrencesInMonth(template, month, monthEnd);

            // Low spend is the pessimistic (most negative) bound, so it drives the low balance.
            balanceLow += expectedNet + typicalSpend.Low;
            balanceMid += expectedNet + typicalSpend.Mid;
            balanceHigh += expectedNet + typicalSpend.High;

            rows.Add(
                new OutlookMonthRow(
                    month,
                    expectedNet,
                    typicalSpend.Low,
                    typicalSpend.Mid,
                    typicalSpend.High,
                    balanceLow,
                    balanceMid,
                    balanceHigh
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// How many times a template's charge lands inside one calendar month. Recurring cadences that
    /// align to a month yield one occurrence; <see cref="Cadence.Weekly"/> yields however many of
    /// its 7-day steps fall inside the month (4 or 5). Respects the optional end date.
    /// </summary>
    private static int OccurrencesInMonth(
        OutlookTemplateSpec template,
        DateOnly month,
        DateOnly monthEnd
    )
    {
        var anchorMonth = FirstOfMonth(template.AnchorDate);
        if (month < anchorMonth)
            return 0;
        if (template.EndDate is { } end && month > FirstOfMonth(end))
            return 0;

        return template.Cadence switch
        {
            Cadence.Once => month == anchorMonth ? 1 : 0,
            Cadence.Monthly => 1,
            Cadence.Quarterly => MonthsBetween(anchorMonth, month) % 3 == 0 ? 1 : 0,
            Cadence.Yearly => month.Month == template.AnchorDate.Month ? 1 : 0,
            Cadence.Weekly => WeeklyOccurrences(template, month, monthEnd),
            _ => 0,
        };
    }

    private static int WeeklyOccurrences(
        OutlookTemplateSpec template,
        DateOnly month,
        DateOnly monthEnd
    )
    {
        // Step forward in 7-day strides from the anchor; count the strides that land in this month
        // and before the optional end date. Bounded by ~5 iterations once aligned.
        var count = 0;
        var date = template.AnchorDate;
        if (date < month)
        {
            var weeks = (month.DayNumber - date.DayNumber) / 7;
            date = date.AddDays(weeks * 7);
            while (date < month)
                date = date.AddDays(7);
        }

        while (date <= monthEnd)
        {
            if (template.EndDate is { } end && date > end)
                break;
            count++;
            date = date.AddDays(7);
        }

        return count;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to) =>
        ((to.Year - from.Year) * 12) + (to.Month - from.Month);

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly LastOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
}

/// <summary>
/// One template's anchor snapshot for the engine. <see cref="Delta"/> is the balance-normalized
/// signed contribution of a single occurrence to the pinned account (inflow positive).
/// </summary>
internal sealed record OutlookTemplateSpec(
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long Delta
);

/// <summary>
/// The monthly <c>Typical spend</c> band as balance-normalized deltas (typically ≤ 0). <see
/// cref="Low"/> is the pessimistic bound (most negative), <see cref="High"/> the optimistic one;
/// <see cref="Mid"/> is the trailing median.
/// </summary>
internal sealed record OutlookSpendBand(long Low, long Mid, long High)
{
    public static OutlookSpendBand Zero { get; } = new(0L, 0L, 0L);
}

/// <summary>
/// One projected month. <see cref="ExpectedNet"/> is the templates' net effect; the
/// <c>Spend*</c> fields are the band applied that month; the <c>End*</c> fields are the running
/// month-end balance band.
/// </summary>
internal sealed record OutlookMonthRow(
    DateOnly Month,
    long ExpectedNet,
    long SpendLow,
    long SpendMid,
    long SpendHigh,
    long EndLow,
    long EndMid,
    long EndHigh
);
