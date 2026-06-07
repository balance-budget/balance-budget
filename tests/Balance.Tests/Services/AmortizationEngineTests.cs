using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Loans;

namespace Balance.Tests.Services;

internal sealed class AmortizationEngineTests
{
    private static readonly LoanPartId PartA = new(Guid.CreateVersion7());
    private static readonly LoanPartId PartB = new(Guid.CreateVersion7());

    private static readonly DateOnly Anchor = new(2026, 7, 1);

    private static AmortizationPartSpec Part(
        LoanRepaymentType type,
        long balance,
        decimal ratePercent,
        int months,
        LoanPartId? id = null,
        DateOnly? fixedUntil = null
    ) =>
        new(
            id ?? PartA,
            type,
            Anchor.AddMonths(months - 1),
            balance,
            [new AmortizationRatePeriodSpec(new DateOnly(2020, 1, 1), ratePercent, fixedUntil)]
        );

    [Test]
    public async Task Annuity_full_term_amortizes_to_zero_with_constant_payment()
    {
        // €300,000 at 3.6% over 30 years: textbook annuity payment is €1,363.94.
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Annuity, 30_000_000, 3.6m, 360)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(360);
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);

        // Constant payment modulo cent rounding, all the way to the final period.
        var payments = rows.Select(r => r.Payment).ToList();
        await Assert.That(payments.Max() - payments.Min()).IsLessThanOrEqualTo(2L);
        await Assert.That(payments[0]).IsEqualTo(136_394L);

        // First month splits into €900.00 interest (300k · 3.6%/12) and the remainder principal.
        await Assert.That(rows[0].Interest).IsEqualTo(90_000L);
        await Assert.That(rows[0].Principal).IsEqualTo(46_394L);
    }

    [Test]
    public async Task Annuity_recomputed_from_any_point_on_a_clean_schedule_keeps_the_payment()
    {
        var full = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Annuity, 30_000_000, 3.6m, 360)],
            Anchor
        );

        // Re-anchor 10 years in, using the schedule's own balance — the engine's core claim.
        var midBalance = full[119].EndBalance;
        var reanchored = AmortizationEngine.Project(
            [
                new AmortizationPartSpec(
                    PartA,
                    LoanRepaymentType.Annuity,
                    Anchor.AddMonths(359),
                    midBalance,
                    [new AmortizationRatePeriodSpec(new DateOnly(2020, 1, 1), 3.6m, null)]
                ),
            ],
            Anchor.AddMonths(120)
        );

        await Assert.That(reanchored).Count().IsEqualTo(240);
        await Assert
            .That(Math.Abs(reanchored[0].Payment - full[120].Payment))
            .IsLessThanOrEqualTo(2L);
        await Assert.That(reanchored[^1].EndBalance).IsEqualTo(0L);
    }

    [Test]
    public async Task Linear_repays_constant_principal_and_declining_interest()
    {
        // €120,000 over 120 months: €1,000.00 principal per month.
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Linear, 12_000_000, 4.8m, 120)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(120);
        await Assert.That(rows.All(r => r.Principal == 100_000L)).IsTrue();
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);

        // Interest declines with the balance: first month 120k·0.4%, second on 119k.
        await Assert.That(rows[0].Interest).IsEqualTo(48_000L);
        await Assert.That(rows[1].Interest).IsEqualTo(47_600L);
    }

    [Test]
    public async Task InterestOnly_keeps_the_balance_flat_until_end_date()
    {
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.InterestOnly, 10_000_000, 2.4m, 24)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(24);
        await Assert.That(rows.All(r => r.Principal == 0L)).IsTrue();
        await Assert.That(rows.All(r => r.Interest == 20_000L)).IsTrue();
        await Assert.That(rows[^1].EndBalance).IsEqualTo(10_000_000L);
    }

    [Test]
    public async Task Rate_change_kinks_the_schedule_at_its_effective_date()
    {
        var spec = new AmortizationPartSpec(
            PartA,
            LoanRepaymentType.Annuity,
            Anchor.AddMonths(119),
            12_000_000,
            [
                new AmortizationRatePeriodSpec(new DateOnly(2020, 1, 1), 3.0m, null),
                new AmortizationRatePeriodSpec(Anchor.AddMonths(12), 5.0m, null),
            ]
        );

        var rows = AmortizationEngine.Project([spec], Anchor);

        // Constant payment within each rate regime, a jump at the boundary.
        await Assert.That(rows[11].Payment).IsEqualTo(rows[0].Payment);
        await Assert.That(rows[12].Payment).IsGreaterThan(rows[11].Payment);
        await Assert.That(Math.Abs(rows[13].Payment - rows[12].Payment)).IsLessThanOrEqualTo(2L);
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);
    }

    [Test]
    public async Task Extra_repayment_with_LowerPayment_drops_the_payment_and_keeps_the_end_date()
    {
        var part = Part(LoanRepaymentType.Annuity, 20_000_000, 4.0m, 240);
        var baseline = AmortizationEngine.Project([part], Anchor);
        var scenario = new AmortizationScenario(
            [new AmortizationExtraRepayment(PartA, Anchor.AddMonths(6), 2_500_000)],
            ExtraRepaymentPolicy.LowerPayment
        );

        var rows = AmortizationEngine.Project([part], Anchor, scenario);

        // Same horizon, lower payment from the month after the repayment, zero at the end.
        await Assert.That(rows).Count().IsEqualTo(240);
        await Assert.That(rows[6].ExtraRepayment).IsEqualTo(2_500_000L);
        await Assert.That(rows[6].Payment).IsEqualTo(baseline[6].Payment);
        await Assert.That(rows[7].Payment).IsLessThan(baseline[7].Payment);
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);

        // The scenario saves interest overall.
        var baselineInterest = baseline.Sum(r => r.Interest);
        var scenarioInterest = rows.Sum(r => r.Interest);
        await Assert.That(scenarioInterest).IsLessThan(baselineInterest);
    }

    [Test]
    public async Task Extra_repayment_with_KeepPayment_holds_the_payment_and_finishes_earlier()
    {
        var part = Part(LoanRepaymentType.Annuity, 20_000_000, 4.0m, 240);
        var baseline = AmortizationEngine.Project([part], Anchor);
        var scenario = new AmortizationScenario(
            [new AmortizationExtraRepayment(PartA, Anchor.AddMonths(6), 2_500_000)],
            ExtraRepaymentPolicy.KeepPayment
        );

        var rows = AmortizationEngine.Project([part], Anchor, scenario);

        await Assert.That(rows.Count).IsLessThan(baseline.Count);
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);

        // Payment held at the baseline trajectory after the extra repayment.
        await Assert.That(Math.Abs(rows[8].Payment - baseline[8].Payment)).IsLessThanOrEqualTo(2L);

        // KeepPayment saves more interest than LowerPayment for the same extra repayment.
        var lowered = AmortizationEngine.Project(
            [part],
            Anchor,
            scenario with
            {
                Policy = ExtraRepaymentPolicy.LowerPayment,
            }
        );
        await Assert.That(rows.Sum(r => r.Interest)).IsLessThan(lowered.Sum(r => r.Interest));
    }

    [Test]
    public async Task Assumed_rate_applies_only_past_the_fixation_boundary()
    {
        var fixedUntil = Anchor.AddMonths(12);
        var part = Part(
            LoanRepaymentType.InterestOnly,
            12_000_000,
            3.0m,
            36,
            fixedUntil: fixedUntil
        );
        var scenario = new AmortizationScenario([], ExtraRepaymentPolicy.LowerPayment, 6.0m);

        var rows = AmortizationEngine.Project([part], Anchor, scenario);

        // 3% while fixed (€30.00 per €12k... per month: 12M·0.25% = 30,000), 6% after.
        await Assert.That(rows[12].Interest).IsEqualTo(30_000L);
        await Assert.That(rows[13].Interest).IsEqualTo(60_000L);
    }

    [Test]
    public async Task Final_period_takes_the_remainder_exactly()
    {
        // A balance that does not divide evenly: the final month absorbs the rounding drift.
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Linear, 1_000_001, 5.0m, 3)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(3);
        await Assert.That(rows.Sum(r => r.Principal)).IsEqualTo(1_000_001L);
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);
    }

    [Test]
    public async Task Single_month_remainder_repays_everything_at_once()
    {
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Annuity, 123_456, 3.6m, 1)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(1);
        await Assert.That(rows[0].Principal).IsEqualTo(123_456L);
        await Assert.That(rows[0].Interest).IsEqualTo(370L); // 123,456 · 0.3% = 370.368 → 370
        await Assert.That(rows[0].EndBalance).IsEqualTo(0L);
    }

    [Test]
    public async Task Zero_rate_annuity_degrades_to_linear()
    {
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Annuity, 1_200_000, 0m, 12)],
            Anchor
        );

        await Assert.That(rows).Count().IsEqualTo(12);
        await Assert.That(rows.All(r => r.Interest == 0L)).IsTrue();
        await Assert.That(rows.All(r => r.Principal == 100_000L)).IsTrue();
        await Assert.That(rows[^1].EndBalance).IsEqualTo(0L);
    }

    [Test]
    public async Task Multi_part_projection_merges_rows_by_period()
    {
        var rows = AmortizationEngine.Project(
            [
                Part(LoanRepaymentType.Annuity, 20_000_000, 3.6m, 24, PartA),
                Part(LoanRepaymentType.InterestOnly, 10_000_000, 2.4m, 12, PartB),
            ],
            Anchor
        );

        // Both parts in the first year, only the annuity afterwards.
        await Assert.That(rows.Count(r => r.Period == Anchor)).IsEqualTo(2);
        await Assert.That(rows.Count(r => r.Period == Anchor.AddMonths(12))).IsEqualTo(1);
        await Assert
            .That(rows.Where(r => r.Period == Anchor).Sum(r => r.Interest))
            .IsEqualTo(60_000L + 20_000L);
    }

    [Test]
    public async Task Zero_balance_part_produces_no_rows()
    {
        var rows = AmortizationEngine.Project(
            [Part(LoanRepaymentType.Annuity, 0, 3.6m, 120)],
            Anchor
        );

        await Assert.That(rows).IsEmpty();
    }

    [Test]
    public async Task Anchor_past_the_end_date_produces_no_rows()
    {
        var part = Part(LoanRepaymentType.Annuity, 1_000_000, 3.6m, 12);

        var rows = AmortizationEngine.Project([part], Anchor.AddMonths(12));

        await Assert.That(rows).IsEmpty();
    }
}
