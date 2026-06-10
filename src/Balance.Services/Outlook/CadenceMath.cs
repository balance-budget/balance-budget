using Balance.Data.Entities.Enums;

namespace Balance.Services.Outlook;

/// <summary>
/// Shared cadence arithmetic for the Outlook feature: the next occurrence on or after a date, and
/// the per-month average of a single occurrence's amount (the list's "≈ /mo" figure). Kept pure
/// and separate from the projection engine, which only needs per-month occurrence counts.
/// </summary>
internal static class CadenceMath
{
    /// <summary>
    /// The next occurrence date on or after <paramref name="onOrAfter"/>, respecting the optional
    /// end date; null when the template has ended or a <see cref="Cadence.Once"/> date is past.
    /// </summary>
    public static DateOnly? NextDueDate(
        Cadence cadence,
        DateOnly anchorDate,
        DateOnly? endDate,
        DateOnly onOrAfter
    )
    {
        if (cadence == Cadence.Once)
            return anchorDate >= onOrAfter && WithinEnd(anchorDate, endDate) ? anchorDate : null;

        var step = cadence switch
        {
            Cadence.Weekly => (Func<DateOnly, DateOnly>)(d => d.AddDays(7)),
            Cadence.Monthly => d => d.AddMonths(1),
            Cadence.Quarterly => d => d.AddMonths(3),
            Cadence.Yearly => d => d.AddYears(1),
            _ => d => d.AddMonths(1),
        };

        var date = anchorDate;
        // Fast-forward without unbounded looping: jump close, then step to the boundary.
        while (date < onOrAfter)
        {
            var next = step(date);
            if (next == date)
                break;
            date = next;
        }

        return WithinEnd(date, endDate) ? date : null;
    }

    /// <summary>
    /// The balance-normalized per-month average of one occurrence's <paramref name="delta"/>. A
    /// <see cref="Cadence.Once"/> template has no recurring monthly figure (returns 0).
    /// </summary>
    public static long MonthlyEquivalent(Cadence cadence, long delta) =>
        cadence switch
        {
            Cadence.Once => 0L,
            Cadence.Weekly => (long)Math.Round(delta * 52m / 12m, MidpointRounding.AwayFromZero),
            Cadence.Monthly => delta,
            Cadence.Quarterly => (long)Math.Round(delta / 3m, MidpointRounding.AwayFromZero),
            Cadence.Yearly => (long)Math.Round(delta / 12m, MidpointRounding.AwayFromZero),
            _ => delta,
        };

    private static bool WithinEnd(DateOnly date, DateOnly? endDate) =>
        endDate is not { } end || date <= end;
}
