using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Optional in-list filters for the per-Account Register. <see cref="PostedAccountId"/> narrows to
/// rows whose focal line is posted to that Account or one of its descendants (only meaningful when
/// it lies inside the viewed subtree); <see cref="CounterAccountId"/> narrows to rows whose entry
/// has at least one *other* line on that Account or one of its descendants. Non-postable accounts
/// therefore mean "the whole subtree" in both filters (ADR-0019). <see cref="From"/> /
/// <see cref="To"/> bound the entry Date inclusively, and <see cref="Status"/> matches the focal
/// line's <see cref="ReconciliationStatus"/>.
/// </summary>
public sealed record RegisterListFilter(
    string? Search,
    AccountId? PostedAccountId,
    AccountId? CounterAccountId,
    DateOnly? From,
    DateOnly? To,
    ReconciliationStatus? Status
)
{
    public static RegisterListFilter None { get; } = new(null, null, null, null, null, null);
}
