using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
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

    public async Task<CounterpartyOutput> CreateAsync(
        string name,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                "Counterparty name is required."
            );
        }

        await EnsureNameAvailableAsync(trimmed, excludingId: null, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var counterparty = new Counterparty
        {
            Id = new CounterpartyId(Guid.CreateVersion7()),
            Name = trimmed,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.Counterparties.Add(counterparty);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(counterparty);
    }

    public async Task<CounterpartyOutput> UpdateAsync(
        CounterpartyId id,
        string? name,
        CancellationToken cancellationToken
    )
    {
        var counterparty =
            await _dbContext.Counterparties.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Counterparty {id} not found."
            );

        if (name is not null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                throw new DomainException(
                    DomainExceptionKind.Validation,
                    "Counterparty name cannot be empty."
                );
            }
            if (!string.Equals(trimmed, counterparty.Name, StringComparison.Ordinal))
            {
                await EnsureNameAvailableAsync(trimmed, excludingId: id, cancellationToken);
            }
            counterparty.Name = trimmed;
        }

        counterparty.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(counterparty);
    }

    public async Task DeleteAsync(CounterpartyId id, CancellationToken cancellationToken)
    {
        var counterparty =
            await _dbContext.Counterparties.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Counterparty {id} not found."
            );

        _dbContext.Counterparties.Remove(counterparty);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"Counterparty {id} is referenced by other records and cannot be deleted.",
                ex
            );
        }
    }

    private static CounterpartyOutput ToOutput(Counterparty counterparty) =>
        new(counterparty.Id, counterparty.Name, counterparty.CreatedAt, counterparty.UpdatedAt);

    private async Task EnsureNameAvailableAsync(
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
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"A counterparty named '{name}' already exists."
            );
        }
    }
}
