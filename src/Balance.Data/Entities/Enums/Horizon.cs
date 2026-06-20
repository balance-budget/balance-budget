namespace Balance.Data.Entities.Enums;

/// <summary>
/// When the holder expects to draw on an account's money — a budgeting judgment orthogonal to
/// <see cref="AccountType"/> and to liquidity, meaningful only on Asset and Liability accounts.
/// Drives the dashboard's tiered balance charts so wildly different magnitudes (a Savings balance
/// dwarfs a Current one) don't share an axis. See ADR-0030 and the Horizon glossary entry.
/// Ordinal: ShortTerm &lt; MediumTerm &lt; LongTerm.
/// </summary>
public enum Horizon
{
    /// <summary>Day-to-day spending money, relevant today (e.g. a Current account).</summary>
    ShortTerm,

    /// <summary>Reserves likely touched this year (e.g. a Savings account).</summary>
    MediumTerm,

    /// <summary>Wealth held for the decade (e.g. real estate, pensions, locked deposits).</summary>
    LongTerm,
}
