namespace Balance.Services.Helpers;

internal static class DateBoundaryExtensions
{
    public static DateOnly GetMonthStart(this DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly GetMonthEnd(this DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    public static DateOnly GetQuarterStart(this DateOnly date)
    {
        var startMonth = ((date.Month - 1) / 3) * 3 + 1;
        return new DateOnly(date.Year, startMonth, 1);
    }

    public static DateOnly GetQuarterEnd(this DateOnly date)
    {
        var endMonth = ((date.Month - 1) / 3) * 3 + 3;
        return new DateOnly(date.Year, endMonth, DateTime.DaysInMonth(date.Year, endMonth));
    }

    public static DateOnly GetYearStart(this DateOnly date) => new(date.Year, 1, 1);

    public static DateOnly GetYearEnd(this DateOnly date) => new(date.Year, 12, 31);

    public static DateTime GetDayStart(this DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    public static DateTime GetDayEnd(this DateOnly date) =>
        DateTime
            .SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            .AddDays(1)
            .AddTicks(-1);
}
