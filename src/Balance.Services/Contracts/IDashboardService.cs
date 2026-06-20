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

    Task<Result<NetWorthTrendOutput>> GetNetWorthTrendAsync(
        CurrencyCode currencyCode,
        NetWorthRange range,
        CancellationToken cancellationToken
    );

    Task<Result<SpendingByCategoryOutput>> GetSpendingByCategoryAsync(
        CurrencyCode currencyCode,
        int topN,
        CancellationToken cancellationToken
    );

    Task<Result<DashboardRegisterPreviewOutput>> GetRegisterPreviewsAsync(
        int rowsPerAccount,
        CancellationToken cancellationToken
    );
}
