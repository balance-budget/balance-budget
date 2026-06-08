using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

/// <summary>
/// One entry in a <see cref="LoanPart"/>'s effective-dated list of interest rates. The rate in
/// force for a month is the latest entry effective on or before it; future-dated entries (an
/// accepted renewal offer) are legitimate and feed the projection. History is appended, never
/// overwritten (ADR-0025).
/// </summary>
public sealed class LoanPartRatePeriod : BaseEntity<LoanPartRatePeriodId>
{
    public required LoanPartId LoanPartId { get; set; }

    public required DateOnly EffectiveDate { get; set; }

    /// <summary>Annual nominal rate in percent (3.8 means 3.8% per year).</summary>
    public required decimal AnnualRatePercent { get; set; }

    /// <summary>
    /// End of the rate-fixation period, when known. Beyond this date
    /// the projection stops being contractual and becomes an assumption.
    /// </summary>
    public DateOnly? FixedUntil { get; set; }
}
