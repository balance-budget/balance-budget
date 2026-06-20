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

    public async Task<PagedOutput<AccountOutput>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await ProjectAccounts(_dbContext.Accounts).ToListAsync(cancellationToken);

        // Roll each non-postable account's balance up from its descendants (ADR-0019). Leaves
        // resolve to their own sum because their subtree is just themselves.
        var nodes = rows.Select(r => new AccountNode(r.Id, r.ParentAccountId)).ToList();
        var ownSums = rows.ToDictionary(r => r.Id, r => r.OwnRawSum);
        var subtreeSums = AccountTree.SubtreeSums(nodes, ownSums);

        var items = rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => ToOutput(r, subtreeSums[r.Id]))
            .ToList();
        return new PagedOutput<AccountOutput>(items, items.Count);
    }

    public async Task<Result<AccountOutput>> GetAsync(
        AccountId id,
        CancellationToken cancellationToken
    )
    {
        var row = await ProjectAccounts(_dbContext.Accounts.Where(a => a.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return new NotFoundError("Account", id.Value.ToString());
        }

        var rolledRawSum = await RolledUpRawSumAsync(id, cancellationToken);
        return ToOutput(row, rolledRawSum);
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
                Code = a.Code,
                AccountType = a.AccountType,
                CurrencyCode = a.CurrencyCode,
                IsPostable = a.IsPostable,
                IsLiquid = a.IsLiquid,
                Horizon = a.Horizon,
                ParentAccountId = a.ParentAccountId,
                IconName = a.IconName,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return snapshot is null ? new NotFoundError("Account", id.Value.ToString()) : snapshot;
    }

    public async Task<Result<AccountOutput>> CreateAsync(
        CreateAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return new InvariantError(ErrorCodes.AccountNameEmpty, "Account name is required.");
        }

        var code = input.Code?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            return new InvariantError(ErrorCodes.AccountCodeEmpty, "Account code is required.");
        }

        var currencyCheck = await EnsureCurrencyExistsAsync(input.CurrencyCode, cancellationToken);
        if (currencyCheck.IsFailure)
            return currencyCheck.Error;

        var codeCheck = await EnsureCodeAvailableAsync(code, excludingId: null, cancellationToken);
        if (codeCheck.IsFailure)
            return codeCheck.Error;

        if (input.ParentAccountId is { } parentId)
        {
            var parentCheck = await ValidateParentAsync(
                parentId,
                input.AccountType,
                input.CurrencyCode,
                cancellationToken
            );
            if (parentCheck.IsFailure)
                return parentCheck.Error;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var account = new Account
        {
            Id = new AccountId(Guid.CreateVersion7()),
            Name = name,
            Code = code,
            AccountType = input.AccountType,
            CurrencyCode = input.CurrencyCode,
            IsPostable = input.IsPostable,
            IsLiquid = input.IsLiquid,
            Horizon = input.Horizon,
            ParentAccountId = input.ParentAccountId,
            IconName = NormalizeIconName(input.IconName),
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
            account.Code,
            account.AccountType,
            account.CurrencyCode,
            account.IsPostable,
            account.IsLiquid,
            account.Horizon,
            account.ParentAccountId,
            account.IconName,
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

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return new InvariantError(ErrorCodes.AccountNameEmpty, "Account name cannot be empty.");
        }

        var code = input.Code?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            return new InvariantError(ErrorCodes.AccountCodeEmpty, "Account code cannot be empty.");
        }

        if (!string.Equals(code, account.Code, StringComparison.Ordinal))
        {
            var codeCheck = await EnsureCodeAvailableAsync(
                code,
                excludingId: id,
                cancellationToken
            );
            if (codeCheck.IsFailure)
                return codeCheck.Error;
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

        var hasChildren = await _dbContext.Accounts.AnyAsync(
            a => a.ParentAccountId == id,
            cancellationToken
        );
        var hasLines = await _dbContext.JournalLines.AnyAsync(
            l => l.AccountId == id,
            cancellationToken
        );

        // Postability conversion gate (ADR-0019): a non-postable account may never carry lines, and
        // a postable account with children is impossible.
        if (input.IsPostable && hasChildren)
        {
            return new InvariantError(
                ErrorCodes.AccountHasChildren,
                "An account with children cannot be postable."
            );
        }

        if (!input.IsPostable && hasLines)
        {
            return new InvariantError(
                ErrorCodes.AccountHasLines,
                "An account with journal lines cannot be made non-postable."
            );
        }

        // Type and currency are fixed once an account participates in a tree, because the whole
        // subtree must stay homogeneous (ADR-0019) and we do not cascade type changes.
        var typeOrCurrencyChanged =
            input.AccountType != account.AccountType || input.CurrencyCode != account.CurrencyCode;
        if (
            typeOrCurrencyChanged
            && (
                hasChildren
                || account.ParentAccountId is not null
                || input.ParentAccountId is not null
            )
        )
        {
            return new InvariantError(
                ErrorCodes.AccountTypeLockedInTree,
                "Account type and currency are fixed while the account is part of an account tree."
            );
        }

        // Re-parenting: validate the new parent and guard against cycles.
        if (
            input.ParentAccountId != account.ParentAccountId
            && input.ParentAccountId is { } newParentId
        )
        {
            if (newParentId == id)
            {
                return new InvariantError(
                    ErrorCodes.AccountParentCycle,
                    "An account cannot be its own parent."
                );
            }

            var nodes = await LoadNodesAsync(cancellationToken);
            if (AccountTree.WouldCreateCycle(nodes, id, newParentId))
            {
                return new InvariantError(
                    ErrorCodes.AccountParentCycle,
                    "An account cannot be moved under its own descendant."
                );
            }

            var parentCheck = await ValidateParentAsync(
                newParentId,
                input.AccountType,
                input.CurrencyCode,
                cancellationToken
            );
            if (parentCheck.IsFailure)
                return parentCheck.Error;
        }

        account.Name = name;
        account.Code = code;
        account.AccountType = input.AccountType;
        account.CurrencyCode = input.CurrencyCode;
        account.IsPostable = input.IsPostable;
        account.IsLiquid = input.IsLiquid;
        account.Horizon = input.Horizon;
        account.ParentAccountId = input.ParentAccountId;
        account.IconName = NormalizeIconName(input.IconName);
        account.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        // Re-query so the response carries the rolled-up Balance and BankAccount summary.
        return await GetAsync(id, cancellationToken);
    }

    public Task<Result> DeleteAsync(AccountId id, CancellationToken cancellationToken) =>
        // A parent with children is blocked by the self-FK RESTRICT, surfaced as a Conflict
        // (ErrorCodes.Referenced) by the DbServiceErrorMapper — same path as a referenced account.
        _dbContext
            .Accounts.Where(c => c.Id == id)
            .DeleteSingleAndCatchAsync("Account", id.Value.ToString(), cancellationToken);

    // Projects into a flat row in SQL carrying each account's OWN raw line sum; callers roll the
    // sums up the tree, then pass each row through ToOutput to flip the sign per AccountType and
    // wrap the Money — neither step translates to SQL (checked() + Money's value-object ctor).
    private IQueryable<AccountProjectionRow> ProjectAccounts(IQueryable<Account> source) =>
        source.Select(a => new AccountProjectionRow(
            a.Id,
            a.Name,
            a.Code,
            a.AccountType,
            a.CurrencyCode,
            a.IsPostable,
            a.IsLiquid,
            a.Horizon,
            a.ParentAccountId,
            a.IconName,
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

    private static AccountOutput ToOutput(AccountProjectionRow row, long rawSum) =>
        new(
            row.Id,
            row.Name,
            row.Code,
            row.AccountType,
            row.CurrencyCode,
            row.IsPostable,
            row.IsLiquid,
            row.Horizon,
            row.ParentAccountId,
            row.IconName,
            AccountSignConvention.ToBalance(row.AccountType, rawSum, row.CurrencyCode),
            row.BankAccount,
            row.CreatedAt,
            row.UpdatedAt
        );

    // The focal account's raw line sum rolled up over its whole subtree (self + descendants).
    private async Task<long> RolledUpRawSumAsync(AccountId id, CancellationToken cancellationToken)
    {
        var nodes = await LoadNodesAsync(cancellationToken);
        var ids = AccountTree.DescendantsAndSelf(nodes, id).ToList();
        return await _dbContext
                .JournalLines.AsNoTracking()
                .Where(l => ids.Contains(l.AccountId))
                .Select(l => (long?)l.Amount)
                .SumAsync(cancellationToken)
            ?? 0L;
    }

    // A blank icon name means "no custom icon" — store null so the avatar falls back to the
    // AccountType default. Icon names are presentation-layer identifiers; see the request
    // validator for why no allowlist is enforced here.
    private static string? NormalizeIconName(string? iconName) =>
        string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();

    private Task<List<AccountNode>> LoadNodesAsync(CancellationToken cancellationToken) =>
        _dbContext
            .Accounts.AsNoTracking()
            .Select(a => new AccountNode(a.Id, a.ParentAccountId))
            .ToListAsync(cancellationToken);

    private async Task<Result> ValidateParentAsync(
        AccountId parentId,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        var parent = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == parentId)
            .Select(a => new
            {
                a.IsPostable,
                a.AccountType,
                a.CurrencyCode,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (parent is null)
        {
            return new NotFoundError("Account", parentId.Value.ToString());
        }

        if (parent.IsPostable)
        {
            return new InvariantError(
                ErrorCodes.AccountParentMustBeNonPostable,
                "The parent account must be non-postable; convert it first."
            );
        }

        if (parent.AccountType != accountType)
        {
            return new InvariantError(
                ErrorCodes.AccountSubtreeTypeMismatch,
                "An account must share its parent's account type."
            );
        }

        if (parent.CurrencyCode != currencyCode)
        {
            return new InvariantError(
                ErrorCodes.AccountSubtreeCurrencyMismatch,
                "An account must share its parent's currency."
            );
        }

        return Result.Success;
    }

    private async Task<Result> EnsureCurrencyExistsAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    )
    {
        var result = await _currencyService.GetAsync(code, cancellationToken);
        return result.IsFailure ? result.Error : Result.Success;
    }

    private Task<Result> EnsureCodeAvailableAsync(
        string code,
        AccountId? excludingId,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .Accounts.Where(a => a.Code == code && (excludingId == null || a.Id != excludingId))
            .EnsureNoneAsync(
                ErrorCodes.AccountCodeTaken,
                $"An account with code '{code}' already exists.",
                cancellationToken
            );

    private sealed record AccountProjectionRow(
        AccountId Id,
        string Name,
        string Code,
        AccountType AccountType,
        CurrencyCode CurrencyCode,
        bool IsPostable,
        bool IsLiquid,
        Horizon Horizon,
        AccountId? ParentAccountId,
        string? IconName,
        long OwnRawSum,
        BankAccountSummary? BankAccount,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
