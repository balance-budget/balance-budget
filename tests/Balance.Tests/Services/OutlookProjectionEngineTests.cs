using Balance.Data.Entities.Enums;
using Balance.Services.Outlook;

namespace Balance.Tests.Services;

internal sealed class OutlookProjectionEngineTests
{
    private const long Opening = 100_000L; // €1,000.00
    private static readonly DateOnly From = new(2026, 7, 1);

    private static OutlookTemplateSpec Template(
        Cadence cadence,
        DateOnly anchor,
        long delta,
        DateOnly? end = null
    ) => new(cadence, anchor, end, delta);

    [Test]
    public async Task Monthly_template_applies_once_per_month_to_the_running_balance()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [Template(Cadence.Monthly, new DateOnly(2026, 1, 1), -5_000L)],
            OutlookSpendBand.Zero,
            From,
            12
        );

        await Assert.That(rows).Count().IsEqualTo(12);
        await Assert.That(rows[0].ExpectedNet).IsEqualTo(-5_000L);
        await Assert.That(rows[0].EndMid).IsEqualTo(95_000L);
        await Assert.That(rows[11].EndMid).IsEqualTo(Opening - (12 * 5_000L));
    }

    [Test]
    public async Task Once_template_lands_only_in_its_anchor_month()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [Template(Cadence.Once, new DateOnly(2026, 9, 1), -20_000L)],
            OutlookSpendBand.Zero,
            From,
            12
        );

        await Assert.That(rows[2].Month).IsEqualTo(new DateOnly(2026, 9, 1));
        await Assert.That(rows[2].ExpectedNet).IsEqualTo(-20_000L);
        await Assert.That(rows[0].ExpectedNet).IsEqualTo(0L);
        await Assert.That(rows[3].ExpectedNet).IsEqualTo(0L);
        // The one-off permanently shifts the curve, but never repeats.
        await Assert.That(rows[11].EndMid).IsEqualTo(Opening - 20_000L);
    }

    [Test]
    public async Task Quarterly_template_recurs_every_third_month_from_the_anchor()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [Template(Cadence.Quarterly, new DateOnly(2026, 7, 1), -3_000L)],
            OutlookSpendBand.Zero,
            From,
            12
        );

        foreach (var index in new[] { 0, 3, 6, 9 })
            await Assert.That(rows[index].ExpectedNet).IsEqualTo(-3_000L);
        foreach (var index in new[] { 1, 2, 4, 5 })
            await Assert.That(rows[index].ExpectedNet).IsEqualTo(0L);
    }

    [Test]
    public async Task Yearly_template_recurs_in_the_anchor_month_each_year()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [Template(Cadence.Yearly, new DateOnly(2026, 7, 1), -120_000L)],
            OutlookSpendBand.Zero,
            From,
            18
        );

        await Assert.That(rows[0].ExpectedNet).IsEqualTo(-120_000L);
        await Assert.That(rows[12].ExpectedNet).IsEqualTo(-120_000L); // July 2027
        await Assert.That(rows[6].ExpectedNet).IsEqualTo(0L);
    }

    [Test]
    public async Task Weekly_template_counts_every_seven_day_step_inside_the_month()
    {
        // 2026-07-06 is a Monday: July has steps on 6,13,20,27 (4); August adds a 5th.
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [Template(Cadence.Weekly, new DateOnly(2026, 7, 6), -1_000L)],
            OutlookSpendBand.Zero,
            From,
            12
        );

        await Assert.That(rows[0].ExpectedNet).IsEqualTo(-4_000L);
        await Assert.That(rows[1].ExpectedNet).IsEqualTo(-5_000L);
    }

    [Test]
    public async Task End_date_stops_occurrences_after_its_month()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [
                Template(
                    Cadence.Monthly,
                    new DateOnly(2026, 1, 1),
                    -5_000L,
                    new DateOnly(2026, 8, 15)
                ),
            ],
            OutlookSpendBand.Zero,
            From,
            12
        );

        await Assert.That(rows[0].ExpectedNet).IsEqualTo(-5_000L); // July
        await Assert.That(rows[1].ExpectedNet).IsEqualTo(-5_000L); // August (end month)
        await Assert.That(rows[2].ExpectedNet).IsEqualTo(0L); // September — past the end
    }

    [Test]
    public async Task Typical_spend_band_spreads_the_balance_low_mid_high()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [],
            new OutlookSpendBand(-9_000L, -6_000L, -3_000L),
            From,
            12
        );

        await Assert.That(rows[0].EndLow).IsEqualTo(91_000L);
        await Assert.That(rows[0].EndMid).IsEqualTo(94_000L);
        await Assert.That(rows[0].EndHigh).IsEqualTo(97_000L);
        await Assert.That(rows[1].EndLow).IsEqualTo(82_000L);
        await Assert.That(rows[1].EndHigh).IsEqualTo(94_000L);
        // The band must never invert: Low ≤ Mid ≤ High every month.
        foreach (var row in rows)
        {
            await Assert.That(row.EndLow).IsLessThanOrEqualTo(row.EndMid);
            await Assert.That(row.EndMid).IsLessThanOrEqualTo(row.EndHigh);
        }
    }

    [Test]
    public async Task Inflow_and_outflow_templates_net_against_each_other()
    {
        var rows = OutlookProjectionEngine.Project(
            Opening,
            [
                Template(Cadence.Monthly, new DateOnly(2026, 1, 1), 400_000L), // salary in
                Template(Cadence.Monthly, new DateOnly(2026, 1, 1), -150_000L), // rent out
            ],
            OutlookSpendBand.Zero,
            From,
            12
        );

        await Assert.That(rows[0].ExpectedNet).IsEqualTo(250_000L);
        await Assert.That(rows[0].EndMid).IsEqualTo(Opening + 250_000L);
    }
}
