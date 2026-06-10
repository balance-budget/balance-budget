using Balance.Data.Entities.Ids;

namespace Balance.Services.Outlook;

/// <summary>
/// The layered matching key (ADR-0027), shared by detection (grouping history) and Typical-spend
/// exclusion (recognizing a posted occurrence as a template's realization). Precise where the data
/// allows — SEPA mandate, then creditor — then falls back to counterparty, then the P&L
/// counter-account. Amount agreement is a band, not equality, because real charges drift.
/// </summary>
internal static class OutlookMatching
{
    /// <summary>The strongest available identity signal as a stable grouping string; null when none.</summary>
    public static string? GroupKey(
        string? mandateId,
        string? sepaCreditorId,
        CounterpartyId? counterpartyId,
        AccountId? counterAccountId
    )
    {
        if (!string.IsNullOrWhiteSpace(mandateId))
            return "m:" + mandateId;
        if (!string.IsNullOrWhiteSpace(sepaCreditorId))
            return "c:" + sepaCreditorId;
        if (counterpartyId is { } cp)
            return "p:" + cp.Value;
        if (counterAccountId is { } ca)
            return "a:" + ca.Value;
        return null;
    }

    /// <summary>The tolerated amount drift around an expected amount: the larger of €5 or 20%.</summary>
    public static long AmountBand(long expectedAmount) =>
        Math.Max(500L, Math.Abs(expectedAmount) / 5);

    public static bool AmountWithinBand(long actual, long expected) =>
        Math.Abs(actual - expected) <= AmountBand(expected);
}
