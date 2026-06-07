namespace Balance.Data.Entities.Enums;

/// <summary>
/// How a Loan Part's principal amortizes over its term (ADR-0025). <see cref="InterestOnly"/>
/// keeps the balance flat until the end date; <see cref="Linear"/> repays a fixed principal
/// amount per month; <see cref="Annuity"/> keeps the combined interest + principal payment
/// constant while the rate holds.
/// </summary>
public enum LoanRepaymentType
{
    InterestOnly,
    Linear,
    Annuity,
}
