using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICounterpartyService
{
    Task<IReadOnlyList<CounterpartyOutput>> ListAsync(CancellationToken cancellationToken);

    Task<CounterpartyOutput?> GetAsync(CounterpartyId id, CancellationToken cancellationToken);

    Task<CounterpartyOutput> CreateAsync(string name, CancellationToken cancellationToken);

    Task<CounterpartyOutput> UpdateAsync(
        CounterpartyId id,
        string? name,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(CounterpartyId id, CancellationToken cancellationToken);
}
