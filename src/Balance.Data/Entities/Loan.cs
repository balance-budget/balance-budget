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

    public Collection<LoanPart> Parts { get; } = [];
}
