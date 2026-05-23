using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyOutput>> ListAsync(CancellationToken cancellationToken);

    Task<Result<CurrencyOutput>> GetAsync(CurrencyCode code, CancellationToken cancellationToken);

    Task<Result<UpdateCurrencyInput>> GetSnapshotAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    );

    Task<Result<CurrencyOutput>> CreateAsync(
        CreateCurrencyInput input,
        CancellationToken cancellationToken
    );

    Task<Result<CurrencyOutput>> UpdateAsync(
        CurrencyCode code,
        UpdateCurrencyInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(CurrencyCode code, CancellationToken cancellationToken);
}
