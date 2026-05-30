using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface ICounterpartyService
{
    Task<PagedOutput<CounterpartyOutput>> ListAsync(
        int skip,
        int? take,
        string? search,
        CancellationToken cancellationToken
    );

    Task<Result<CounterpartyOutput>> GetAsync(
        CounterpartyId id,
        CancellationToken cancellationToken
    );

    Task<Result<UpdateCounterpartyInput>> GetSnapshotAsync(
        CounterpartyId id,
        CancellationToken cancellationToken
    );

    Task<Result<CounterpartyOutput>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Result<CounterpartyOutput>> UpdateAsync(
        CounterpartyId id,
        UpdateCounterpartyInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(CounterpartyId id, CancellationToken cancellationToken);
}

public sealed record UpdateCounterpartyInput
{
    public required string Name { get; set; }
}
