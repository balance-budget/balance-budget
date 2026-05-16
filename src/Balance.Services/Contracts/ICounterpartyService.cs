using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICounterpartyService
{
    Task<IReadOnlyList<Counterparty>> ListAsync(CancellationToken cancellationToken);

    Task<Counterparty?> GetAsync(CounterpartyId id, CancellationToken cancellationToken);

    Task<Counterparty> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Counterparty> UpdateAsync(
        CounterpartyId id,
        string? name,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(CounterpartyId id, CancellationToken cancellationToken);
}
