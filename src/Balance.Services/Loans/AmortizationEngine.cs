using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Loans;

/// <summary>
/// The single amortization implementation (ADR-0025): a pure function from part definitions,
/// effective-dated rate periods, and anchor balances to projected per-month, per-part rows.
/// Anchor-snapshot math, never inception replay — for an annuity, recomputing the payment from
/// any point on a clean schedule yields the same payment, which is what makes an extra repayment
/// need zero event machinery under the Dutch-default policy (end date fixed, payment lowered).
/// All amounts are minor units; monthly interest is the annual nominal rate ÷ 12 on the balance
/// at period start, rounded to the cent.
/// </summary>
internal static class AmortizationEngine
{
    /// <summary>
    /// Projects every part from <paramref name="anchorMonth"/> (normalized to the first of the
    /// month) through its end-date month, or until its balance reaches zero. Rows are ordered by
    /// period, then by the order parts were supplied in.
    /// </summary>
    public static IReadOnlyList<AmortizationPeriodRow> Project(
        IReadOnlyList<AmortizationPartSpec> parts,
        DateOnly anchorMonth,
        AmortizationScenario? scenario = null
    )
    {
        ArgumentNullException.ThrowIfNull(parts);

        var start = FirstOfMonth(anchorMonth);
        var rowsPerPart = new List<List<AmortizationPeriodRow>>(parts.Count);
        foreach (var part in parts)
        {
            // KeepPayment holds the baseline payment trajectory while the balance drops faster,
            // so the part finishes earlier; the baseline run is what defines that trajectory.
            IReadOnlyList<AmortizationPeriodRow>? baseline = null;
            if (scenario is { Policy: ExtraRepaymentPolicy.KeepPayment })
                baseline = ProjectPart(part, start, scenario: null, baseline: null);

            rowsPerPart.Add(ProjectPart(part, start, scenario, baseline));
        }

        return MergeByPeriod(rowsPerPart);
    }

    private static List<AmortizationPeriodRow> ProjectPart(
        AmortizationPartSpec part,
        DateOnly start,
        AmortizationScenario? scenario,
        IReadOnlyList<AmortizationPeriodRow>? baseline
    )
    {
        var rows = new List<AmortizationPeriodRow>();
        var endMonth = FirstOfMonth(part.EndDate);
        var balance = part.AnchorBalance;

        for (var month = start; month <= endMonth; month = month.AddMonths(1))
        {
            if (balance <= 0)
                break;

            var annualRatePercent = RateInForce(part.RatePeriods, month, scenario);
            var monthlyRate = annualRatePercent / 100m / 12m;
            var interest = RoundToCent(balance * monthlyRate);
            var remainingMonths = MonthsBetweenInclusive(month, endMonth);

            var principal = baseline is null
                ? ScheduledPrincipal(part.RepaymentType, balance, monthlyRate, remainingMonths)
                : BaselinePrincipal(baseline, month, balance, interest);
            principal = Math.Min(principal, balance);

            var extra = scenario is null
                ? 0L
                : Math.Min(ExtraRepaymentFor(scenario, part.PartId, month), balance - principal);

            balance -= principal + extra;
            rows.Add(
                new AmortizationPeriodRow(
                    month,
                    part.PartId,
                    interest,
                    principal,
                    extra,
                    interest + principal,
                    balance
                )
            );
        }

        return rows;
    }

    private static long ScheduledPrincipal(
        LoanRepaymentType repaymentType,
        long balance,
        decimal monthlyRate,
        int remainingMonths
    )
    {
        if (repaymentType == LoanRepaymentType.InterestOnly)
            return 0L;

        if (remainingMonths <= 1)
            return balance;

        if (repaymentType == LoanRepaymentType.Linear)
            return RoundToCent((decimal)balance / remainingMonths);

        // Annuity: payment = B·r / (1 − (1+r)^−n), recomputed every month from the current
        // balance — on a clean schedule this reproduces the original constant payment, and after
        // a rate change or extra repayment it *is* the recalculation.
        if (monthlyRate == 0m)
            return RoundToCent((decimal)balance / remainingMonths);

        var payment = RoundToCent(
            balance * monthlyRate / (1m - Pow(1m + monthlyRate, remainingMonths))
        );
        return Math.Max(0L, payment - RoundToCent(balance * monthlyRate));
    }

    // KeepPayment: spend the baseline month's total payment; whatever interest doesn't consume
    // goes to principal. Months past the baseline's horizon keep the last baseline payment so a
    // shrunken balance still drains at the original pace.
    private static long BaselinePrincipal(
        IReadOnlyList<AmortizationPeriodRow> baseline,
        DateOnly month,
        long balance,
        long interest
    )
    {
        if (baseline.Count == 0)
            return balance;

        var baselineRow = baseline.FirstOrDefault(r => r.Period == month) ?? baseline[^1];
        if (baselineRow.Principal == 0L && baselineRow.ExtraRepayment == 0L)
            return 0L; // interest-only stays interest-only

        return Math.Min(balance, Math.Max(0L, baselineRow.Payment - interest));
    }

    private static decimal RateInForce(
        IReadOnlyList<AmortizationRatePeriodSpec> ratePeriods,
        DateOnly month,
        AmortizationScenario? scenario
    )
    {
        if (ratePeriods.Count == 0)
            return 0m;

        AmortizationRatePeriodSpec? inForce = null;
        foreach (var period in ratePeriods)
        {
            if (period.EffectiveDate > month)
                continue;

            if (inForce is null || period.EffectiveDate > inForce.EffectiveDate)
                inForce = period;
        }

        // Anchor predates every known rate period: fall back to the earliest one.
        inForce ??= ratePeriods.OrderBy(p => p.EffectiveDate).First();

        // Past the fixation boundary the contractual rate is an assumption; the simulator may
        // substitute its own. Only applies when no later explicit period has taken over.
        if (
            scenario is { AssumedAnnualRatePercent: { } assumed }
            && inForce.FixedUntil is { } fixedUntil
            && month > fixedUntil
        )
        {
            return assumed;
        }

        return inForce.AnnualRatePercent;
    }

    private static long ExtraRepaymentFor(
        AmortizationScenario scenario,
        LoanPartId partId,
        DateOnly month
    )
    {
        var sum = 0L;
        foreach (var extra in scenario.ExtraRepayments)
        {
            if (extra.PartId == partId && FirstOfMonth(extra.Date) == month)
                sum += extra.Amount;
        }

        return Math.Max(0L, sum);
    }

    private static IReadOnlyList<AmortizationPeriodRow> MergeByPeriod(
        List<List<AmortizationPeriodRow>> rowsPerPart
    )
    {
        var merged = new List<AmortizationPeriodRow>(rowsPerPart.Sum(r => r.Count));
        foreach (var rows in rowsPerPart)
            merged.AddRange(rows);

        return [.. merged.OrderBy(r => r.Period)];
    }

    private static long RoundToCent(decimal value) =>
        (long)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static decimal Pow(decimal baseValue, int negativeExponent)
    {
        // (1+r)^−n via repeated division keeps everything in decimal; n is bounded by the part's
        // remaining term in months, so the loop is small.
        var result = 1m;
        for (var i = 0; i < negativeExponent; i++)
            result /= baseValue;

        return result;
    }

    private static int MonthsBetweenInclusive(DateOnly from, DateOnly to) =>
        (to.Year - from.Year) * 12 + (to.Month - from.Month) + 1;

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}

/// <summary>One part's anchor snapshot: what the engine projects from (ADR-0025).</summary>
internal sealed record AmortizationPartSpec(
    LoanPartId PartId,
    LoanRepaymentType RepaymentType,
    DateOnly EndDate,
    long AnchorBalance,
    IReadOnlyList<AmortizationRatePeriodSpec> RatePeriods
);

internal sealed record AmortizationRatePeriodSpec(
    DateOnly EffectiveDate,
    decimal AnnualRatePercent,
    DateOnly? FixedUntil
);

/// <summary>
/// An ephemeral what-if overlay: hypothetical extra repayments, the repayment policy to apply,
/// and optionally a rate to assume once a part's fixation period lapses. Never persisted.
/// </summary>
internal sealed record AmortizationScenario(
    IReadOnlyList<AmortizationExtraRepayment> ExtraRepayments,
    ExtraRepaymentPolicy Policy,
    decimal? AssumedAnnualRatePercent = null
);

internal sealed record AmortizationExtraRepayment(LoanPartId PartId, DateOnly Date, long Amount);

/// <summary>
/// How an extra repayment reshapes the schedule. <see cref="LowerPayment"/> is the Dutch default:
/// the end date stays fixed and the payment drops. <see cref="KeepPayment"/> holds the payment
/// and finishes earlier — a simulator-only lever (ADR-0025).
/// </summary>
public enum ExtraRepaymentPolicy
{
    LowerPayment,
    KeepPayment,
}

/// <summary>
/// One part-month of the projection. <see cref="Payment"/> is the regular debit (interest +
/// scheduled principal); <see cref="ExtraRepayment"/> is reported separately and reflected in
/// <see cref="EndBalance"/>.
/// </summary>
internal sealed record AmortizationPeriodRow(
    DateOnly Period,
    LoanPartId PartId,
    long Interest,
    long Principal,
    long ExtraRepayment,
    long Payment,
    long EndBalance
);
