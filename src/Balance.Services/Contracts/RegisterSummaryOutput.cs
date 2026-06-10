using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The bucket size of a <b>Register summary</b> (see CONTEXT.md). Chosen by the caller — the
/// client derives it from the requested date range — never inferred server-side.
/// </summary>
public enum RegisterSummaryBucket
{
    Day,
    Week,
    Month,
}

/// <summary>
/// A <b>Register summary</b> (see CONTEXT.md): one <b>Register</b> aggregated into time buckets,
/// segmented by the focal account's direct children (deeper descendants roll up into their
/// direct-child ancestor; a postable leaf yields a single segment — itself). Amounts are net per
/// segment per bucket, normalized to the account's normal balance per ADR-0011, in minor units of
/// <see cref="CurrencyCode"/> (the whole subtree shares one currency, ADR-0019). Buckets cover
/// the full range gaplessly — an empty bucket has no values but still appears.
/// </summary>
public sealed record RegisterSummaryOutput(
    RegisterSummaryBucket Bucket,
    DateOnly From,
    DateOnly To,
    CurrencyCode CurrencyCode,
    IReadOnlyList<RegisterSummarySegment> Segments,
    IReadOnlyList<RegisterSummaryBucketOutput> Buckets
);

/// <summary>
/// One stack segment of a <b>Register summary</b> — a direct child of the focal account (or the
/// focal account itself on a leaf). Only segments with at least one non-zero bucket value appear,
/// ordered by chart-of-accounts <c>Code</c>.
/// </summary>
public sealed record RegisterSummarySegment(AccountId AccountId, string AccountName);

/// <summary>
/// One time bucket: <see cref="Start"/> is the bucket's first day (the day itself, the ISO-week
/// Monday, or the first of the month). <see cref="Values"/> carries one entry per segment with a
/// non-zero net amount in this bucket.
/// </summary>
public sealed record RegisterSummaryBucketOutput(
    DateOnly Start,
    IReadOnlyList<RegisterSummaryValue> Values
);

public sealed record RegisterSummaryValue(AccountId AccountId, long Amount);
