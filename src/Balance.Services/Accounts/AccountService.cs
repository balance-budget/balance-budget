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

    public async Task<IReadOnlyList<AccountOutput>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await ProjectAccounts(_dbContext.Accounts.OrderBy(a => a.Name))
            .ToListAsync(cancellationToken);
        return rows.Select(ToOutput).ToList();
    }

    public async Task<AccountOutput?> GetAsync(AccountId id, CancellationToken cancellationToken)
    {
        var row = await ProjectAccounts(_dbContext.Accounts.Where(a => a.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToOutput(row);
    }

    public Task<UpdateAccountInput?> GetSnapshotAsync(
        AccountId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new UpdateAccountInput
            {
                Name = a.Name,
                AccountType = a.AccountType,
                CurrencyCode = a.CurrencyCode,
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AccountOutput> CreateAsync(
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
        return new AccountOutput(
            account.Id,
            account.Name,
            account.AccountType,
            account.CurrencyCode,
            Money.Zero(account.CurrencyCode),
            BankAccount: null,
            account.CreatedAt,
            account.UpdatedAt
        );
    }

    public async Task<AccountOutput> UpdateAsync(
        AccountId id,
        UpdateAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var account =
            await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new DomainException(DomainExceptionKind.NotFound, $"Account {id} not found.");

        var trimmed = input.Name?.Trim() ?? string.Empty;
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

        if (input.CurrencyCode != account.CurrencyCode)
        {
            await EnsureCurrencyExistsAsync(input.CurrencyCode, cancellationToken);
        }

        account.Name = trimmed;
        account.AccountType = input.AccountType;
        account.CurrencyCode = input.CurrencyCode;
        account.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
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

    private IQueryable<AccountProjectionRow> ProjectAccounts(IQueryable<Account> source) =>
        source.Select(a => new AccountProjectionRow(
            a.Id,
            a.Name,
            a.AccountType,
            a.CurrencyCode,
            _dbContext.JournalLines.Where(l => l.AccountId == a.Id).Sum(l => (long?)l.Amount) ?? 0L,
            _dbContext
                .BankAccounts.Where(ba => ba.AccountId == a.Id)
                .Select(ba => new BankAccountSummary(
                    ba.Iban,
                    ba.AccountNumber,
                    ba.Bic,
                    ba.BankName
                ))
                .FirstOrDefault(),
            a.CreatedAt,
            a.UpdatedAt
        ));

    private static AccountOutput ToOutput(AccountProjectionRow row) =>
        new(
            row.Id,
            row.Name,
            row.AccountType,
            row.CurrencyCode,
            new Money(
                AccountSignConvention.IsCreditNormal(row.AccountType)
                    ? checked(-row.RawSum)
                    : row.RawSum,
                row.CurrencyCode
            ),
            row.BankAccount,
            row.CreatedAt,
            row.UpdatedAt
        );

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

    private sealed record AccountProjectionRow(
        AccountId Id,
        string Name,
        AccountType AccountType,
        CurrencyCode CurrencyCode,
        long RawSum,
        BankAccountSummary? BankAccount,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
