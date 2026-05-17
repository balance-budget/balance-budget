using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyOutput>> ListAsync(CancellationToken cancellationToken);

    Task<CurrencyOutput?> GetAsync(CurrencyCode code, CancellationToken cancellationToken);

    Task<CurrencyOutput> CreateAsync(
        CreateCurrencyInput input,
        CancellationToken cancellationToken
    );

    Task<CurrencyOutput> UpdateAsync(
        CurrencyCode code,
        UpdateCurrencyInput input,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(CurrencyCode code, CancellationToken cancellationToken);
}
