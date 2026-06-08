using Balance.Data.Entities.Ids;
using Balance.Services.Loans;

namespace Balance.Services.Contracts;

/// <summary>
/// Read-side of the Loan domain (ADR-0025): the amortization engine applied to live ledger
/// anchors. Nothing here is materialized — every call recomputes from (balance now, rate now,
/// remaining term), which is also what makes posted extra repayments self-recalculate.
/// </summary>
public interface ILoanProjectionService
{
    /// <summary>
    /// The engine-computed payment proposal for one month, feeding the loan-aware categorise
    /// pre-fill. Amounts are editable defaults — the bank's actual charge wins.
    /// </summary>
    Task<Result<LoanPaymentProposalOutput>> GetPaymentProposalAsync(
        LoanId id,
        DateOnly month,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Ledger actuals (left of today) plus the baseline projection (right of today), with an
    /// optional ephemeral what-if scenario overlaid and its totals reported.
    /// </summary>
    Task<Result<LoanProjectionOutput>> GetProjectionAsync(
        LoanId id,
        LoanScenarioInput? scenario,
        CancellationToken cancellationToken
    );
}

public sealed record LoanPaymentProposalOutput(
    LoanId LoanId,
    DateOnly Month,
    CurrencyCode CurrencyCode,
    AccountId InterestExpenseAccountId,
    IReadOnlyList<LoanPaymentProposalLineOutput> Lines,
    long Total,
    LoanDepositOffsetOutput? DepositOffset
);

/// <summary>
/// The deposit-interest offset to pre-fill on the loan payment during construction (ADR-0026):
/// an Income credit on <see cref="IncomeAccountId"/> of <see cref="Amount"/> minor units, so the
/// entry's net matches the single netted debit the lender collects. Null when the loan has no
/// Construction deposit or its balance is zero. <see cref="Total"/> on the proposal stays gross
/// (sum of part payments); the net is <c>Total − DepositOffset.Amount</c>.
/// </summary>
public sealed record LoanDepositOffsetOutput(AccountId IncomeAccountId, long Amount);

public sealed record LoanPaymentProposalLineOutput(
    LoanPartId LoanPartId,
    string Label,
    AccountId PartAccountId,
    long Interest,
    long Principal,
    long Payment
);

/// <summary>An ephemeral simulator scenario; never persisted (ADR-0025).</summary>
public sealed record LoanScenarioInput(
    IReadOnlyList<LoanScenarioExtraRepaymentInput> ExtraRepayments,
    ExtraRepaymentPolicy Policy,
    decimal? AssumedAnnualRatePercent
);

public sealed record LoanScenarioExtraRepaymentInput(
    LoanPartId LoanPartId,
    DateOnly Date,
    long Amount
);

public sealed record LoanProjectionOutput(
    LoanId LoanId,
    CurrencyCode CurrencyCode,
    DateOnly AnchorMonth,
    IReadOnlyList<LoanProjectionPartOutput> Parts,
    IReadOnlyList<LoanPeriodRowOutput> Actuals,
    IReadOnlyList<LoanPeriodRowOutput> Baseline,
    IReadOnlyList<LoanPeriodRowOutput>? Scenario,
    LoanScenarioTotalsOutput? Totals
);

/// <summary>
/// <see cref="FixedUntil"/> is the rate-fixation boundary of the rate in force at the anchor —
/// where the projection stops being contractual and becomes an assumption.
/// </summary>
public sealed record LoanProjectionPartOutput(
    LoanPartId Id,
    string Label,
    AccountId AccountId,
    DateOnly? FixedUntil
);

public sealed record LoanPeriodRowOutput(
    DateOnly Period,
    LoanPartId LoanPartId,
    long Interest,
    long Principal,
    long ExtraRepayment,
    long Payment,
    long EndBalance
);

/// <summary>
/// <see cref="NextPaymentDelta"/> compares the month after the first hypothetical extra
/// repayment (scenario minus baseline, so negative means a lower payment).
/// </summary>
public sealed record LoanScenarioTotalsOutput(
    long InterestSaved,
    long NextPaymentDelta,
    DateOnly? BaselineEndDate,
    DateOnly? ScenarioEndDate
);
