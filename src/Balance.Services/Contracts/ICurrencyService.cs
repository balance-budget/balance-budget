using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICurrencyService
{
    Task<IReadOnlyList<Currency>> ListAsync(CancellationToken cancellationToken);

    Task<Currency?> GetAsync(CurrencyCode code, CancellationToken cancellationToken);
}
