using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyOutput>> ListAsync(CancellationToken cancellationToken);

    Task<CurrencyOutput?> GetAsync(CurrencyCode code, CancellationToken cancellationToken);
}
