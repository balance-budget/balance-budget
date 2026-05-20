using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record AccountBalanceTrendOutput(
    IReadOnlyList<AccountTrendSeries> Series,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    TrendRange Range,
    CurrencyCode CurrencyCode
);

public sealed record AccountTrendSeries(
    AccountId AccountId,
    string AccountName,
    IReadOnlyList<TrendPoint> Points
);

public sealed record TrendPoint(DateOnly Date, Money Balance);
