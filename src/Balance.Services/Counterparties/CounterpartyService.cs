using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Counterparties;

internal sealed class CounterpartyService : ICounterpartyService
{
    private const string NameUniqueIndex = "IX_Counterparties_Name";

    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CounterpartyService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<Counterparty>> ListAsync(CancellationToken cancellationToken) =>
        await _dbContext
            .Counterparties.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<Counterparty?> GetAsync(CounterpartyId id, CancellationToken cancellationToken) =>
        _dbContext
            .Counterparties.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Counterparty> CreateAsync(string name, CancellationToken cancellationToken)
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

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var counterparty = new Counterparty
        {
            Id = new CounterpartyId(Guid.CreateVersion7()),
            Name = trimmed,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.Counterparties.Add(counterparty);
        await SaveChangesHandlingUniqueAsync(trimmed, cancellationToken);
        return counterparty;
    }

    public async Task<Counterparty> UpdateAsync(
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

        string? renamedTo = null;
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
            counterparty.Name = trimmed;
            renamedTo = trimmed;
        }

        counterparty.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await SaveChangesHandlingUniqueAsync(renamedTo ?? counterparty.Name, cancellationToken);
        return counterparty;
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

    private async Task SaveChangesHandlingUniqueAsync(
        string conflictingName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsNameUniqueViolation(ex))
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"A counterparty named '{conflictingName}' already exists.",
                ex
            );
        }
    }

    private static bool IsNameUniqueViolation(DbUpdateException ex)
    {
        for (var current = ex.InnerException; current is not null; current = current.InnerException)
        {
            if (
                current.Message.Contains(NameUniqueIndex, StringComparison.Ordinal)
                || current.Message.Contains("Counterparties.Name", StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }
}
