using System.Collections.ObjectModel;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

/// <summary>
/// A borrowing agreement with a lender — a mortgage, a personal or car loan — layered over the
/// ledger (ADR-0025). Represented in the chart of accounts as one non-postable Liability parent
/// <see cref="Account"/> (<see cref="ParentAccountId"/>) with one postable child Account per
/// <see cref="LoanPart"/>; the loan-level outstanding debt is the parent's roll-up balance. The
/// loan domain never stores a competing balance figure.
/// </summary>
public sealed class Loan : BaseEntity<LoanId>
{
    public required string Name { get; set; }

    /// <summary>The lender; drives the Inbox's loan-payment hint.</summary>
    public required CounterpartyId LenderCounterpartyId { get; set; }

    /// <summary>
    /// The single postable Expense account that receives every interest line of this loan, so
    /// per-loan interest stays separable in reports.
    /// </summary>
    public required AccountId InterestExpenseAccountId { get; set; }

    /// <summary>The non-postable Liability parent account representing the loan.</summary>
    public required AccountId ParentAccountId { get; set; }

    /// <summary>
    /// Optional Construction deposit (Dutch <em>bouwdepot</em>, ADR-0026): an Asset account holding
    /// mortgage money the lender has not yet disbursed. A plain, non-loan-managed account; the loan
    /// references it only to compute the deposit-interest offset on its payment. Set together with
    /// <see cref="ConstructionDepositInterestIncomeAccountId"/> and
    /// <see cref="ConstructionDepositAnnualRatePercent"/>, or all three null.
    /// </summary>
    public AccountId? ConstructionDepositAccountId { get; set; }

    /// <summary>The Income account credited by the deposit-interest offset (ADR-0026).</summary>
    public AccountId? ConstructionDepositInterestIncomeAccountId { get; set; }

    /// <summary>The annual nominal rate the Construction deposit earns; ÷12 per month (ADR-0026).</summary>
    public decimal? ConstructionDepositAnnualRatePercent { get; set; }

    public Collection<LoanPart> Parts { get; } = [];
}
