using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IDashboardService
{
    Task<Result<DashboardSummaryOutput>> GetSummaryAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<Result<AccountBalanceTrendOutput>> GetAccountBalanceTrendAsync(
        CurrencyCode currencyCode,
        TrendRange range,
        CancellationToken cancellationToken
    );

    Task<Result<DashboardRecentActivityOutput>> GetRecentActivityAsync(
        int rowsPerAccount,
        CancellationToken cancellationToken
    );
}
