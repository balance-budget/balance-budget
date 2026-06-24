using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Accounts;

/// <summary>
/// The account-creation invariants in one place: name/code presence and code
/// uniqueness across both committed rows and same-transaction pending inserts.
/// The factory only <em>stages</em> the entity in the change tracker — the
/// caller owns <c>SaveChanges</c>, so several accounts can be created in one
/// unit of work (e.g. a loan parent plus its parts, whose codes must not
/// collide with each other before any of them is persisted).
/// </summary>
internal sealed class AccountFactory
{
    private readonly BalanceDbContext _dbContext;

    public AccountFactory(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Account>> StageAsync(
        NewAccount spec,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(spec);

        var name = spec.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return new InvariantError(ErrorCodes.AccountNameEmpty, "Account name cannot be empty.");

        var code = spec.Code?.Trim() ?? string.Empty;
        if (code.Length == 0)
            return new InvariantError(ErrorCodes.AccountCodeEmpty, "Account code cannot be empty.");

        var availability = await EnsureCodeAvailableAsync(code, cancellationToken);
        if (availability.IsFailure)
            return availability.Error;

        var account = new Account
        {
            Id = new AccountId(Guid.CreateVersion7()),
            Name = name,
            Code = code,
            AccountType = spec.AccountType,
            CurrencyCode = spec.CurrencyCode,
            IsPostable = spec.IsPostable,
            IsLiquid = spec.IsLiquid,
            Horizon = spec.Horizon,
            ParentAccountId = spec.ParentAccountId,
            IconName = spec.IconName,
            CreatedAt = spec.Now,
            UpdatedAt = spec.Now,
        };
        _dbContext.Accounts.Add(account);
        return account;
    }

    private async Task<Result> EnsureCodeAvailableAsync(
        string code,
        CancellationToken cancellationToken
    )
    {
        var committed = await _dbContext
            .Accounts.Where(a => a.Code == code)
            .EnsureNoneAsync(
                ErrorCodes.AccountCodeTaken,
                $"Account code '{code}' is already taken.",
                cancellationToken
            );
        if (committed.IsFailure)
            return committed.Error;

        // Codes staged earlier in the same unit of work aren't visible to the query above.
        var pendingCollision = _dbContext
            .ChangeTracker.Entries<Account>()
            .Any(e => e.State == EntityState.Added && e.Entity.Code == code);
        return pendingCollision
            ? new ConflictError(
                ErrorCodes.AccountCodeTaken,
                $"Account code '{code}' is already taken."
            )
            : Result.Success;
    }
}

/// <summary>Specification for a new account staged through <see cref="AccountFactory"/>.</summary>
internal sealed record NewAccount(
    string Name,
    string Code,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    bool IsPostable,
    bool IsLiquid,
    Horizon Horizon,
    AccountId? ParentAccountId,
    string? IconName,
    DateTime Now
);
