using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountService
{
    Task<PagedOutput<AccountOutput>> ListAsync(CancellationToken cancellationToken);

    Task<Result<AccountOutput>> GetAsync(AccountId id, CancellationToken cancellationToken);

    Task<Result<UpdateAccountInput>> GetSnapshotAsync(
        AccountId id,
        CancellationToken cancellationToken
    );

    Task<Result<AccountOutput>> CreateAsync(
        CreateAccountInput input,
        CancellationToken cancellationToken
    );

    Task<Result<AccountOutput>> UpdateAsync(
        AccountId id,
        UpdateAccountInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(AccountId id, CancellationToken cancellationToken);
}

public sealed record CreateAccountInput
{
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required AccountType AccountType { get; init; }
    public required CurrencyCode CurrencyCode { get; init; }
    public bool IsPostable { get; init; } = true;

    /// <summary>
    /// Whether the account counts toward liquid net worth. Meaningful only on Asset and
    /// Liability accounts; accepted and ignored on other types.
    /// </summary>
    public bool IsLiquid { get; init; } = true;
    public AccountId? ParentAccountId { get; init; }
    public string? IconName { get; init; }
}

public sealed record UpdateAccountInput
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required AccountType AccountType { get; set; }
    public required CurrencyCode CurrencyCode { get; set; }
    public required bool IsPostable { get; set; }
    public required bool IsLiquid { get; set; }
    public AccountId? ParentAccountId { get; set; }
    public string? IconName { get; set; }
}
