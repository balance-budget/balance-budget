using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IDashboardSummaryService
{
    Task<DashboardSummaryOutput> GetSummaryAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );
}
