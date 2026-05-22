using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Counterparties;

internal sealed class CounterpartyService : ICounterpartyService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CounterpartyService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<CounterpartyOutput>> ListAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .Counterparties.OrderBy(c => c.Name)
            .Select(c => new CounterpartyOutput(c.Id, c.Name, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);

    public Task<CounterpartyOutput?> GetAsync(
        CounterpartyId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .Counterparties.Where(c => c.Id == id)
            .Select(c => new CounterpartyOutput(c.Id, c.Name, c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UpdateCounterpartyInput?> GetSnapshotAsync(
        CounterpartyId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .Counterparties.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new UpdateCounterpartyInput { Name = c.Name })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Result<CounterpartyOutput>> CreateAsync(
        string name,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return new InvariantError(
                ErrorCodes.CounterpartyNameEmpty,
                "Counterparty name is required."
            );
        }

        if (
            await EnsureNameAvailableAsync(trimmed, excludingId: null, cancellationToken) is
            { Error: { } e1 }
        )
        {
            return e1;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var counterparty = new Counterparty
        {
            Id = new CounterpartyId(Guid.CreateVersion7()),
            Name = trimmed,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.Counterparties.Add(counterparty);
        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } e2 })
        {
            return e2;
        }
        return ToOutput(counterparty);
    }

    public async Task<Result<CounterpartyOutput>> UpdateAsync(
        CounterpartyId id,
        UpdateCounterpartyInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var counterparty = await _dbContext.Counterparties.FirstOrDefaultAsync(
            c => c.Id == id,
            cancellationToken
        );
        if (counterparty is null)
        {
            return new NotFoundError("Counterparty", id.Value.ToString());
        }

        var trimmed = input.Name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new InvariantError(
                ErrorCodes.CounterpartyNameEmpty,
                "Counterparty name cannot be empty."
            );
        }

        if (!string.Equals(trimmed, counterparty.Name, StringComparison.Ordinal))
        {
            if (
                await EnsureNameAvailableAsync(trimmed, excludingId: id, cancellationToken) is
                { Error: { } e1 }
            )
            {
                return e1;
            }
        }

        counterparty.Name = trimmed;
        counterparty.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } e2 })
        {
            return e2;
        }
        return ToOutput(counterparty);
    }

    public async Task<Result> DeleteAsync(CounterpartyId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .Counterparties.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("Counterparty", id.Value.ToString());

        return Result.Success;
    }

    private static CounterpartyOutput ToOutput(Counterparty counterparty) =>
        new(counterparty.Id, counterparty.Name, counterparty.CreatedAt, counterparty.UpdatedAt);

    private async Task<Result> EnsureNameAvailableAsync(
        string name,
        CounterpartyId? excludingId,
        CancellationToken cancellationToken
    )
    {
        var taken = await _dbContext.Counterparties.AnyAsync(
            c => c.Name == name && (excludingId == null || c.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            return new ConflictError(
                ErrorCodes.CounterpartyNameTaken,
                $"A counterparty named '{name}' already exists."
            );
        }
        return Result.Success;
    }
}
