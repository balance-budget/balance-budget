using System.Collections.ObjectModel;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

/// <summary>
/// One component (Dutch: leningdeel) of a <see cref="Loan"/>, carrying its own repayment type,
/// term, and effective-dated rate history. Represented by exactly one postable Liability
/// <see cref="Account"/> (<see cref="AccountId"/>) under the loan's parent account; the part's
/// outstanding principal <em>is</em> that account's balance (ADR-0025).
/// </summary>
public sealed class LoanPart : BaseEntity<LoanPartId>
{
    public required LoanId LoanId { get; set; }

    /// <summary>The part number or label as it appears on the lender's statement.</summary>
    public required string Label { get; set; }

    public required LoanRepaymentType RepaymentType { get; set; }

    public required DateOnly StartDate { get; set; }

    public required DateOnly EndDate { get; set; }

    /// <summary>The postable Liability child account whose balance is this part's principal.</summary>
    public required AccountId AccountId { get; set; }

    public Collection<LoanPartRatePeriod> RatePeriods { get; } = [];
}
