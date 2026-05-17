using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Accounts;

internal sealed class AccountService : IAccountService
{
    private const string NameUniqueIndex = "IX_Accounts_Name";

    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AccountService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) =>
        await _dbContext
            .Accounts.AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public Task<Account?> GetAsync(AccountId id, CancellationToken cancellationToken) =>
        _dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Account> CreateAsync(
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(DomainExceptionKind.Validation, "Account name is required.");
        }

        await EnsureCurrencyExistsAsync(currencyCode, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var account = new Account
        {
            Id = new AccountId(Guid.CreateVersion7()),
            Name = trimmed,
            AccountType = accountType,
            CurrencyCode = currencyCode,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.Accounts.Add(account);
        await SaveChangesHandlingUniqueAsync(trimmed, cancellationToken);
        return account;
    }

    public async Task<Account> UpdateAsync(
        AccountId id,
        string? name,
        AccountType? accountType,
        CurrencyCode? currencyCode,
        CancellationToken cancellationToken
    )
    {
        var account =
            await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new DomainException(DomainExceptionKind.NotFound, $"Account {id} not found.");

        string? renamedTo = null;
        if (name is not null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                throw new DomainException(
                    DomainExceptionKind.Validation,
                    "Account name cannot be empty."
                );
            }
            account.Name = trimmed;
            renamedTo = trimmed;
        }

        if (accountType is not null)
        {
            account.AccountType = accountType.Value;
        }

        if (currencyCode is not null)
        {
            await EnsureCurrencyExistsAsync(currencyCode.Value, cancellationToken);
            account.CurrencyCode = currencyCode.Value;
        }

        account.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await SaveChangesHandlingUniqueAsync(renamedTo ?? account.Name, cancellationToken);
        return account;
    }

    public async Task DeleteAsync(AccountId id, CancellationToken cancellationToken)
    {
        var account =
            await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new DomainException(DomainExceptionKind.NotFound, $"Account {id} not found.");

        _dbContext.Accounts.Remove(account);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"Account {id} is referenced by other records and cannot be deleted.",
                ex
            );
        }
    }

    private async Task EnsureCurrencyExistsAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    )
    {
        var exists = await _dbContext.Currencies.AnyAsync(c => c.Code == code, cancellationToken);
        if (!exists)
        {
            throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{code.Value}' is not defined."
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
                $"An account named '{conflictingName}' already exists.",
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
                || current.Message.Contains("Accounts.Name", StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }
}
