using Balance.Services.Helpers;

namespace Balance.Tests.Domain;

internal sealed class DateBoundaryExtensionsTests
{
    [Test]
    public async Task GetMonthStart_returns_first_day_of_month()
    {
        await Assert
            .That(new DateOnly(2026, 5, 20).GetMonthStart())
            .IsEqualTo(new DateOnly(2026, 5, 1));
    }

    [Test]
    public async Task GetMonthStart_on_first_of_month_returns_same_day()
    {
        await Assert
            .That(new DateOnly(2026, 5, 1).GetMonthStart())
            .IsEqualTo(new DateOnly(2026, 5, 1));
    }

    [Test]
    public async Task GetMonthEnd_returns_31st_for_31_day_month()
    {
        await Assert
            .That(new DateOnly(2026, 1, 15).GetMonthEnd())
            .IsEqualTo(new DateOnly(2026, 1, 31));
    }

    [Test]
    public async Task GetMonthEnd_returns_30th_for_30_day_month()
    {
        await Assert
            .That(new DateOnly(2026, 4, 1).GetMonthEnd())
            .IsEqualTo(new DateOnly(2026, 4, 30));
    }

    [Test]
    public async Task GetMonthEnd_returns_28th_for_non_leap_february()
    {
        await Assert
            .That(new DateOnly(2025, 2, 10).GetMonthEnd())
            .IsEqualTo(new DateOnly(2025, 2, 28));
    }

    [Test]
    public async Task GetMonthEnd_returns_29th_for_leap_february()
    {
        await Assert
            .That(new DateOnly(2024, 2, 10).GetMonthEnd())
            .IsEqualTo(new DateOnly(2024, 2, 29));
    }

    [Test]
    [Arguments(1, 1, 1)]
    [Arguments(2, 1, 1)]
    [Arguments(3, 1, 1)]
    [Arguments(4, 4, 1)]
    [Arguments(5, 4, 1)]
    [Arguments(6, 4, 1)]
    [Arguments(7, 7, 1)]
    [Arguments(8, 7, 1)]
    [Arguments(9, 7, 1)]
    [Arguments(10, 10, 1)]
    [Arguments(11, 10, 1)]
    [Arguments(12, 10, 1)]
    public async Task GetQuarterStart_returns_first_day_of_quarter(
        int inputMonth,
        int expectedMonth,
        int expectedDay
    )
    {
        await Assert
            .That(new DateOnly(2026, inputMonth, 15).GetQuarterStart())
            .IsEqualTo(new DateOnly(2026, expectedMonth, expectedDay));
    }

    [Test]
    [Arguments(1, 3, 31)]
    [Arguments(2, 3, 31)]
    [Arguments(3, 3, 31)]
    [Arguments(4, 6, 30)]
    [Arguments(5, 6, 30)]
    [Arguments(6, 6, 30)]
    [Arguments(7, 9, 30)]
    [Arguments(8, 9, 30)]
    [Arguments(9, 9, 30)]
    [Arguments(10, 12, 31)]
    [Arguments(11, 12, 31)]
    [Arguments(12, 12, 31)]
    public async Task GetQuarterEnd_returns_last_day_of_quarter(
        int inputMonth,
        int expectedMonth,
        int expectedDay
    )
    {
        await Assert
            .That(new DateOnly(2026, inputMonth, 15).GetQuarterEnd())
            .IsEqualTo(new DateOnly(2026, expectedMonth, expectedDay));
    }

    [Test]
    public async Task GetQuarterStart_on_first_day_returns_same_day()
    {
        await Assert
            .That(new DateOnly(2026, 7, 1).GetQuarterStart())
            .IsEqualTo(new DateOnly(2026, 7, 1));
    }

    [Test]
    public async Task GetQuarterEnd_on_last_day_returns_same_day()
    {
        await Assert
            .That(new DateOnly(2026, 12, 31).GetQuarterEnd())
            .IsEqualTo(new DateOnly(2026, 12, 31));
    }

    [Test]
    public async Task GetYearStart_returns_jan_1()
    {
        await Assert
            .That(new DateOnly(2026, 5, 20).GetYearStart())
            .IsEqualTo(new DateOnly(2026, 1, 1));
    }

    [Test]
    public async Task GetYearEnd_returns_dec_31()
    {
        await Assert
            .That(new DateOnly(2026, 5, 20).GetYearEnd())
            .IsEqualTo(new DateOnly(2026, 12, 31));
    }

    [Test]
    public async Task GetYearEnd_returns_dec_31_for_leap_year()
    {
        await Assert
            .That(new DateOnly(2024, 2, 29).GetYearEnd())
            .IsEqualTo(new DateOnly(2024, 12, 31));
    }

    [Test]
    public async Task GetDayStart_returns_midnight_utc()
    {
        var dayStart = new DateOnly(2026, 5, 20).GetDayStart();

        await Assert.That(dayStart).IsEqualTo(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(dayStart.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task GetDayEnd_returns_last_tick_of_day_utc()
    {
        var dayEnd = new DateOnly(2026, 5, 20).GetDayEnd();

        await Assert
            .That(dayEnd)
            .IsEqualTo(
                new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1)
            );
        await Assert.That(dayEnd.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    // SPLM (same-period-last-month) composition: priorPeriodEnd is `today.AddMonths(-1)`
    // and priorPeriodStart is `priorPeriodEnd.GetMonthStart()`. DateOnly.AddMonths clamps
    // the day-of-month to the last existing day in the prior month (Mar 31 → Feb 28/29),
    // so this composition handles end-of-month edges natively.

    [Test]
    public async Task SplmComposition_for_mid_month_uses_equivalent_day_last_month()
    {
        var today = new DateOnly(2026, 5, 20);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        await Assert.That(priorPeriodStart).IsEqualTo(new DateOnly(2026, 4, 1));
        await Assert.That(priorPeriodEnd).IsEqualTo(new DateOnly(2026, 4, 20));
    }

    [Test]
    public async Task SplmComposition_for_mar_31_clamps_to_feb_28_in_non_leap_year()
    {
        var today = new DateOnly(2025, 3, 31);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        await Assert.That(priorPeriodStart).IsEqualTo(new DateOnly(2025, 2, 1));
        await Assert.That(priorPeriodEnd).IsEqualTo(new DateOnly(2025, 2, 28));
    }

    [Test]
    public async Task SplmComposition_for_mar_31_clamps_to_feb_29_in_leap_year()
    {
        var today = new DateOnly(2024, 3, 31);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        await Assert.That(priorPeriodStart).IsEqualTo(new DateOnly(2024, 2, 1));
        await Assert.That(priorPeriodEnd).IsEqualTo(new DateOnly(2024, 2, 29));
    }

    [Test]
    public async Task SplmComposition_for_jan_15_crosses_year_boundary()
    {
        var today = new DateOnly(2026, 1, 15);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        await Assert.That(priorPeriodStart).IsEqualTo(new DateOnly(2025, 12, 1));
        await Assert.That(priorPeriodEnd).IsEqualTo(new DateOnly(2025, 12, 15));
    }
}
