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
    private readonly BalanceDbContext _dbContext;
    private readonly ICurrencyService _currencyService;
    private readonly TimeProvider _timeProvider;

    public AccountService(
        BalanceDbContext dbContext,
        ICurrencyService currencyService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyService = currencyService;
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
        await EnsureNameAvailableAsync(trimmed, excludingId: null, cancellationToken);

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
        await _dbContext.SaveChangesAsync(cancellationToken);
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
            if (!string.Equals(trimmed, account.Name, StringComparison.Ordinal))
            {
                await EnsureNameAvailableAsync(trimmed, excludingId: id, cancellationToken);
            }
            account.Name = trimmed;
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
        await _dbContext.SaveChangesAsync(cancellationToken);
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
        if (await _currencyService.GetAsync(code, cancellationToken) is null)
        {
            throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{code.Value}' is not defined."
            );
        }
    }

    private async Task EnsureNameAvailableAsync(
        string name,
        AccountId? excludingId,
        CancellationToken cancellationToken
    )
    {
        var taken = await _dbContext.Accounts.AnyAsync(
            a => a.Name == name && (excludingId == null || a.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"An account named '{name}' already exists."
            );
        }
    }
}
