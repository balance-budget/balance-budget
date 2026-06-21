using Balance.Data.Entities.Enums;

namespace Balance.Services.Outlook;

/// <summary>
/// The pure forward half of the liquid-balance <c>Projection</c> (ADR-0027, anchored on the current
/// month per ADR-0028, cone model per ADR-0033): from an opening balance, a set of
/// <see cref="JournalEntryTemplate"/> specs, and a <c>Typical spend</c> model (a median monthly
/// everyday spend plus a robust spread), it produces one month-end balance band per future month.
/// The first month is the <em>current, partial</em> month: it counts only occurrences still due on
/// or after <c>fromDate</c> (earlier ones are already in the opening balance) and prorates the
/// everyday spend by the fraction of the month still remaining. The uncertainty band widens as a
/// <em>random walk</em> — its half-width at month <c>n</c> is <c>spread × √(elapsed months)</c>, not
/// linearly — so good and bad months partly cancel rather than the worst month recurring forever.
/// All amounts are minor units, <em>balance-normalized</em> to the account's own perspective (inflow
/// positive, outflow negative) — the service applies the Sign convention before calling in.
/// </summary>
internal static class OutlookProjectionEngine
{
    /// <summary>
    /// Projects <paramref name="horizonMonths"/> whole months starting at the month of
    /// <paramref name="fromDate"/>. The first month is partial (from <paramref name="fromDate"/> to
    /// month-end); every later month is whole. Each month adds its expected template occurrences and
    /// the median everyday spend to the running center balance; the band around it is
    /// <c>± spread × √(cumulative elapsed months)</c>.
    /// </summary>
    public static IReadOnlyList<OutlookMonthRow> Project(
        long openingBalance,
        IReadOnlyList<OutlookTemplateSpec> templates,
        OutlookSpendModel typicalSpend,
        DateOnly fromDate,
        int horizonMonths
    )
    {
        ArgumentNullException.ThrowIfNull(templates);

        var start = FirstOfMonth(fromDate);
        var rows = new List<OutlookMonthRow>(horizonMonths);

        // The center balance carries templates plus cumulative median everyday spend; the cone
        // half-width is driven by the cumulative count of elapsed month-equivalents (√n growth).
        var centerBalance = openingBalance;
        var elapsedMonths = 0.0;

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
            var fraction = isCurrent ? RemainingFraction(fromDate, monthEnd) : 1.0;
            elapsedMonths += fraction;

            // This month's standalone everyday-spend band (drives the "rest of this month" card).
            var spendMid = Scale(typicalSpend.Median, fraction);
            var spendHalf = HalfWidth(typicalSpend.Spread, fraction);
            var spendLow = spendMid - spendHalf;
            var spendHigh = spendMid + spendHalf;

            // The running center and the random-walk cone around it.
            centerBalance += expectedNet + spendMid;
            var coneHalf = HalfWidth(typicalSpend.Spread, elapsedMonths);

            rows.Add(
                new OutlookMonthRow(
                    month,
                    expectedIn,
                    expectedOut,
                    expectedNet,
                    spendLow,
                    spendMid,
                    spendHigh,
                    centerBalance - coneHalf,
                    centerBalance,
                    centerBalance + coneHalf
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// The fraction of the month from <paramref name="fromDate"/> through month-end (inclusive of
    /// the current day), capped at a whole month, so the current month's everyday spend reflects
    /// only the days still to come.
    /// </summary>
    private static double RemainingFraction(DateOnly fromDate, DateOnly monthEnd)
    {
        var daysInMonth = monthEnd.Day;
        var remaining = daysInMonth - fromDate.Day + 1;
        return remaining >= daysInMonth ? 1.0 : (double)remaining / daysInMonth;
    }

    private static long Scale(long value, double fraction) =>
        fraction >= 1.0 ? value : (long)Math.Round(value * fraction, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The random-walk half-width: <c>spread × √months</c>. Variance of independent monthly draws
    /// adds linearly, so the standard deviation — and thus the band's half-width — grows with √n.
    /// </summary>
    private static long HalfWidth(long spread, double months) =>
        spread <= 0 || months <= 0
            ? 0L
            : (long)Math.Round(spread * Math.Sqrt(months), MidpointRounding.AwayFromZero);

    /// <summary>
    /// The occurrence dates of a template still due in the current (partial) month — those on or
    /// after <paramref name="fromDate"/>. Used to itemize "what's still expected this month".
    /// </summary>
    public static IReadOnlyList<DateOnly> RemainingOccurrences(
        OutlookTemplateSpec template,
        DateOnly fromDate
    )
    {
        var month = FirstOfMonth(fromDate);
        var monthEnd = LastOfMonth(month);
        return [.. OccurrenceDates(template, month, monthEnd).Where(d => d >= fromDate)];
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
/// The <c>Typical spend</c> model for one account (ADR-0033), balance-normalized: <see
/// cref="Median"/> is the trailing-window median monthly everyday spend (typically ≤ 0), and <see
/// cref="Spread"/> is a non-negative robust dispersion (a scaled median-absolute-deviation,
/// i.e. a robust standard-deviation estimate) that sizes the random-walk uncertainty cone.
/// </summary>
internal sealed record OutlookSpendModel(long Median, long Spread)
{
    public static OutlookSpendModel Zero { get; } = new(0L, 0L);
}

/// <summary>
/// One projected month. <see cref="ExpectedIn"/> / <see cref="ExpectedOut"/> split the templates'
/// signed effect by direction (<see cref="ExpectedNet"/> is their sum); the <c>Spend*</c> fields
/// are this month's standalone everyday-spend band (prorated for the current month); the
/// <c>End*</c> fields are the running month-end balance with the random-walk cone around it.
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
