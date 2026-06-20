using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Monthly net-worth trend for the dashboard's long-horizon chart (ADR-0030): two aggregate
/// lines, total <see cref="NetWorthPoint.NetWorth"/> and <see cref="NetWorthPoint.LiquidNetWorth"/>,
/// sampled at one point per month. The gap between the lines is illiquid net worth (e.g. a house
/// amortizing against its mortgage). Amounts are in the envelope currency's minor units.
/// </summary>
public sealed record NetWorthTrendOutput(
    IReadOnlyList<NetWorthPoint> Points,
    NetWorthRange Range,
    CurrencyCode CurrencyCode
);

/// <summary>
/// One month's net worth, as of <see cref="AsOf"/> (month-end, or today for the current month).
/// </summary>
public sealed record NetWorthPoint(DateOnly AsOf, long NetWorth, long LiquidNetWorth);

/// <summary>Horizon of the net-worth chart. Monthly granularity throughout.</summary>
public enum NetWorthRange
{
    OneYear,
    ThreeYears,
    All,
}
