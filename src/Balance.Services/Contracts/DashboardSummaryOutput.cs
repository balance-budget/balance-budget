using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record DashboardSummaryOutput(
    Money NetWorth,
    Money IncomeMtd,
    Money ExpensesMtd,
    Money IncomeMtdPrior,
    Money ExpensesMtdPrior,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    CurrencyCode CurrencyCode
);
