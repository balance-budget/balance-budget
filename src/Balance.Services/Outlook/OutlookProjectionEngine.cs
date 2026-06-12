using Balance.Data.Entities.Enums;

namespace Balance.Services.Outlook;

/// <summary>
/// The pure forward half of the liquid-balance <c>Projection</c> (ADR-0027, anchored on the current
/// month per ADR-0028): from an opening balance, a set of <see cref="JournalEntryTemplate"/> specs,
/// and a <c>Typical spend</c> band, it produces one month-end balance band per future month. The
/// first month is the <em>current, partial</em> month: it counts only occurrences still due on or
/// after <c>fromDate</c> (earlier ones are already in the opening balance) and prorates the everyday
/// <c>Typical spend</c> band by the fraction of the month still remaining. All amounts are minor
/// units, <em>balance-normalized</em> to the account's own perspective (inflow positive, outflow
/// negative) — the service applies the Sign convention before calling in.
/// </summary>
internal static class OutlookProjectionEngine
{
    /// <summary>
    /// Projects <paramref name="horizonMonths"/> whole months starting at the month of
    /// <paramref name="fromDate"/>. The first month is partial (from <paramref name="fromDate"/> to
    /// month-end); every later month is whole. Each month accumulates the expected template
    /// occurrences landing in it plus the <c>Typical spend</c> band onto the running balance.
    /// </summary>
    public static IReadOnlyList<OutlookMonthRow> Project(
        long openingBalance,
        IReadOnlyList<OutlookTemplateSpec> templates,
        OutlookSpendBand typicalSpend,
        DateOnly fromDate,
        int horizonMonths
    )
    {
        ArgumentNullException.ThrowIfNull(templates);

        var start = FirstOfMonth(fromDate);
        var rows = new List<OutlookMonthRow>(horizonMonths);

        var balanceLow = openingBalance;
        var balanceMid = openingBalance;
        var balanceHigh = openingBalance;

        for (var i = 0; i < horizonMonths; i++)
        {
            var month = start.AddMonths(i);
            var monthEnd = LastOfMonth(month);
            var isCurrent = i == 0;

            var expectedIn = 0L;
            var expectedOut = 0L;
            foreach (var template in templates)
            {
                var occurrences = isCurrent
                    ? OccurrenceDates(template, month, monthEnd).Count(d => d >= fromDate)
                    : OccurrenceDates(template, month, monthEnd).Count();
                var contribution = template.Delta * occurrences;
                if (contribution >= 0)
                    expectedIn += contribution;
                else
                    expectedOut += contribution;
            }
            var expectedNet = expectedIn + expectedOut;

            // The current month only sees the everyday spend of the days still ahead.
            var band = isCurrent ? Prorate(typicalSpend, fromDate, monthEnd) : typicalSpend;

            // Low spend is the pessimistic (most negative) bound, so it drives the low balance.
            balanceLow += expectedNet + band.Low;
            balanceMid += expectedNet + band.Mid;
            balanceHigh += expectedNet + band.High;

            rows.Add(
                new OutlookMonthRow(
                    month,
                    expectedIn,
                    expectedOut,
                    expectedNet,
                    band.Low,
                    band.Mid,
                    band.High,
                    balanceLow,
                    balanceMid,
                    balanceHigh
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Scales a whole-month <c>Typical spend</c> band down to the portion of the month from
    /// <paramref name="fromDate"/> through month-end (inclusive of the current day), so the current
    /// month's everyday spend reflects only the days still to come.
    /// </summary>
    private static OutlookSpendBand Prorate(
        OutlookSpendBand band,
        DateOnly fromDate,
        DateOnly monthEnd
    )
    {
        var daysInMonth = monthEnd.Day;
        var remaining = daysInMonth - fromDate.Day + 1;
        if (remaining >= daysInMonth)
            return band;

        long Scale(long value) =>
            (long)
                Math.Round((decimal)value * remaining / daysInMonth, MidpointRounding.AwayFromZero);

        return new OutlookSpendBand(Scale(band.Low), Scale(band.Mid), Scale(band.High));
    }

    /// <summary>
    /// The dates a template's charge lands on inside one calendar month. Recurring cadences that
    /// align to a month yield their nominal day; <see cref="Cadence.Weekly"/> yields every 7-day
    /// step that falls inside the month. Respects the optional end date and the anchor month.
    /// </summary>
    private static IEnumerable<DateOnly> OccurrenceDates(
        OutlookTemplateSpec template,
        DateOnly month,
        DateOnly monthEnd
    )
    {
        var anchorMonth = FirstOfMonth(template.AnchorDate);
        if (month < anchorMonth)
            yield break;
        if (template.EndDate is { } end && month > FirstOfMonth(end))
            yield break;

        switch (template.Cadence)
        {
            case Cadence.Once:
                if (month == anchorMonth && WithinEnd(template.AnchorDate, template.EndDate))
                    yield return template.AnchorDate;
                break;
            case Cadence.Monthly:
                foreach (var d in Nominal(template, month, monthEnd))
                    yield return d;
                break;
            case Cadence.Quarterly:
                if (MonthsBetween(anchorMonth, month) % 3 == 0)
                    foreach (var d in Nominal(template, month, monthEnd))
                        yield return d;
                break;
            case Cadence.Yearly:
                if (month.Month == template.AnchorDate.Month)
                    foreach (var d in Nominal(template, month, monthEnd))
                        yield return d;
                break;
            case Cadence.Weekly:
                foreach (var d in WeeklyDates(template, month, monthEnd))
                    yield return d;
                break;
            default:
                break;
        }
    }

    /// <summary>The template's nominal day-of-month charge, clamped to the month length and bounded by the end date.</summary>
    private static IEnumerable<DateOnly> Nominal(
        OutlookTemplateSpec template,
        DateOnly month,
        DateOnly monthEnd
    )
    {
        var day = Math.Min(template.AnchorDate.Day, monthEnd.Day);
        var date = new DateOnly(month.Year, month.Month, day);
        if (WithinEnd(date, template.EndDate))
            yield return date;
    }

    private static IEnumerable<DateOnly> WeeklyDates(
        OutlookTemplateSpec template,
        DateOnly month,
        DateOnly monthEnd
    )
    {
        // Step forward in 7-day strides from the anchor; yield the strides that land in this month
        // and before the optional end date. Bounded by ~5 iterations once aligned.
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
            yield return date;
            date = date.AddDays(7);
        }
    }

    private static bool WithinEnd(DateOnly date, DateOnly? endDate) =>
        endDate is not { } end || date <= end;

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
/// One projected month. <see cref="ExpectedIn"/> / <see cref="ExpectedOut"/> split the templates'
/// signed effect by direction (<see cref="ExpectedNet"/> is their sum); the <c>Spend*</c> fields
/// are the band applied that month (prorated for the current month); the <c>End*</c> fields are the
/// running month-end balance band.
/// </summary>
internal sealed record OutlookMonthRow(
    DateOnly Month,
    long ExpectedIn,
    long ExpectedOut,
    long ExpectedNet,
    long SpendLow,
    long SpendMid,
    long SpendHigh,
    long EndLow,
    long EndMid,
    long EndHigh
);
