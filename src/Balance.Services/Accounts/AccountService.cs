using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
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

    public async Task<Result<AccountOutput>> GetAsync(
        AccountId id,
        CancellationToken cancellationToken
    )
    {
        var row = await ProjectAccounts(_dbContext.Accounts.Where(a => a.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? new NotFoundError("Account", id.Value.ToString()) : ToOutput(row);
    }

    public async Task<Result<UpdateAccountInput>> GetSnapshotAsync(
        AccountId id,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new UpdateAccountInput
            {
                Name = a.Name,
                AccountType = a.AccountType,
                CurrencyCode = a.CurrencyCode,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return snapshot is null ? new NotFoundError("Account", id.Value.ToString()) : snapshot;
    }

    public async Task<Result<AccountOutput>> CreateAsync(
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
            return new InvariantError(ErrorCodes.AccountNameEmpty, "Account name is required.");
        }

        var currencyCheck = await EnsureCurrencyExistsAsync(currencyCode, cancellationToken);
        if (currencyCheck.IsFailure)
            return currencyCheck.Error;

        var nameCheck = await EnsureNameAvailableAsync(
            trimmed,
            excludingId: null,
            cancellationToken
        );
        if (nameCheck.IsFailure)
            return nameCheck.Error;

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
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

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

    public async Task<Result<AccountOutput>> UpdateAsync(
        AccountId id,
        UpdateAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == id,
            cancellationToken
        );
        if (account is null)
        {
            return new NotFoundError("Account", id.Value.ToString());
        }

        var trimmed = input.Name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new InvariantError(ErrorCodes.AccountNameEmpty, "Account name cannot be empty.");
        }

        if (!string.Equals(trimmed, account.Name, StringComparison.Ordinal))
        {
            var nameCheck = await EnsureNameAvailableAsync(
                trimmed,
                excludingId: id,
                cancellationToken
            );
            if (nameCheck.IsFailure)
                return nameCheck.Error;
        }

        if (input.CurrencyCode != account.CurrencyCode)
        {
            var currencyCheck = await EnsureCurrencyExistsAsync(
                input.CurrencyCode,
                cancellationToken
            );
            if (currencyCheck.IsFailure)
                return currencyCheck.Error;
        }

        account.Name = trimmed;
        account.AccountType = input.AccountType;
        account.CurrencyCode = input.CurrencyCode;
        account.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(AccountId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .Accounts.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("Account", id.Value.ToString());

        return Result.Success;
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

    private async Task<Result> EnsureCurrencyExistsAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    )
    {
        var result = await _currencyService.GetAsync(code, cancellationToken);
        return result.IsFailure ? result.Error : Result.Success;
    }

    private async Task<Result> EnsureNameAvailableAsync(
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
            return new ConflictError(
                ErrorCodes.AccountNameTaken,
                $"An account named '{name}' already exists."
            );
        }

        return Result.Success;
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
