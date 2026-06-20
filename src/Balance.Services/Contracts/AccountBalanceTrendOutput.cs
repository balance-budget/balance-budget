using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record AccountBalanceTrendOutput(
    IReadOnlyList<AccountTrendSeries> Series,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    TrendRange Range,
    CurrencyCode CurrencyCode
);

/// <summary>
/// Per-account trend data in delta form: the balance at <see cref="AccountBalanceTrendOutput.PeriodStart"/>
/// plus a sparse list of daily activity. The renderer reconstructs the daily running balance
/// by walking from <c>PeriodStart</c> to <c>PeriodEnd</c>, adding any matching delta on each
/// day. Currency is on the envelope; all amounts are in that currency's minor units.
/// </summary>
public sealed record AccountTrendSeries(
    AccountId AccountId,
    string AccountName,
    Horizon Horizon,
    long OpeningBalance,
    IReadOnlyList<TrendDelta> Deltas
);

public sealed record TrendDelta(DateOnly Date, long Amount);
