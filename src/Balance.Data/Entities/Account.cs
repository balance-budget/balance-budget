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
    /// Whether the account counts toward liquid net worth — money available for day-to-day
    /// budgeting. A user judgment, meaningful only on Asset and Liability accounts; other types
    /// carry the default and ignore it. Exempt from the subtree homogeneity rule (ADR-0019), so
    /// children in one subtree may differ.
    /// </summary>
    public bool IsLiquid { get; set; } = true;

    /// <summary>
    /// Self-reference forming the chart-of-accounts tree; <c>null</c> for a root account.
    /// </summary>
    public AccountId? ParentAccountId { get; set; }

    /// <summary>
    /// The user-chosen icon name (kebab-case, e.g. <c>piggy-bank</c>) shown wherever the account's
    /// avatar renders; <c>null</c> inherits the <see cref="AccountType"/>'s default icon. Purely
    /// presentational — the avatar's color always derives from the type and is not stored.
    /// </summary>
    public string? IconName { get; set; }
}
