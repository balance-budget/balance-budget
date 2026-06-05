using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class Account : BaseEntity<AccountId>
{
    public required string Name { get; set; }

    /// <summary>
    /// The required, globally-unique chart-of-accounts code (e.g. <c>5110</c>). The human key for an
    /// account; <see cref="Name"/> carries no uniqueness. See ADR-0019.
    /// </summary>
    public required string Code { get; set; }

    public required AccountType AccountType { get; set; }
    public required CurrencyCode CurrencyCode { get; set; }

    /// <summary>
    /// <c>true</c> for a leaf that <c>JournalLine</c>s may reference directly; <c>false</c> for a
    /// non-postable account whose balance is the roll-up of its descendants. An account with
    /// children is never postable (ADR-0019).
    /// </summary>
    public required bool IsPostable { get; set; }

    /// <summary>
    /// Self-reference forming the chart-of-accounts tree; <c>null</c> for a root account.
    /// </summary>
    public AccountId? ParentAccountId { get; set; }
}
