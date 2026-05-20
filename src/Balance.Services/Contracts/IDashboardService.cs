using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IDashboardService
{
    Task<DashboardSummaryOutput> GetSummaryAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<AccountBalanceTrendOutput> GetAccountBalanceTrendAsync(
        CurrencyCode currencyCode,
        TrendRange range,
        CancellationToken cancellationToken
    );
}
