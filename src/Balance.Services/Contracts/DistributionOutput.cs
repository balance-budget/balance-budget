using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// One level of a <c>Distribution</c> report (see CONTEXT.md): the <b>Net movement</b> of each
/// <b>Income</b> or <b>Expense</b> account over the <b>Reporting period</b>, rolled up the
/// chart-of-accounts tree. When <see cref="ParentAccountId"/> is null the slices are the top-level
/// (root) accounts of the requested <see cref="Type"/>; otherwise they are the direct children of
/// that parent (one drill-down level). Amounts are net (refunds reduce an expense slice) and
/// sign-converted to the focal-user perspective per ADR-0012, so income and expense both read as
/// positive in the common case. <see cref="Total"/> is the signed sum of <see cref="Slices"/>.
/// </summary>
public sealed record DistributionOutput(
    DistributionType Type,
    DateOnly From,
    DateOnly To,
    CurrencyCode CurrencyCode,
    AccountId? ParentAccountId,
    Money Total,
    IReadOnlyList<DistributionSlice> Slices
);

/// <summary>
/// One slice of a <see cref="DistributionOutput"/>: a single account's rolled-up Net movement over
/// the period. <see cref="Amount"/> is sign-converted (positive = the normal direction for the
/// account type); a net-negative slice — a subtree whose refunds outweighed its spend in the period
/// — keeps its negative sign so the renderer can exclude it from the part-of-whole chart and surface
/// it as a note instead. <see cref="HasChildren"/> tells the client whether the slice can be drilled
/// into.
/// </summary>
public sealed record DistributionSlice(
    AccountId AccountId,
    string Name,
    string Code,
    Money Amount,
    bool HasChildren
);
