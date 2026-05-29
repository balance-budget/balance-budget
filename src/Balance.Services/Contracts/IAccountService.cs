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
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<Result<AccountOutput>> UpdateAsync(
        AccountId id,
        UpdateAccountInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(AccountId id, CancellationToken cancellationToken);
}

public sealed record UpdateAccountInput
{
    public required string Name { get; set; }
    public required AccountType AccountType { get; set; }
    public required CurrencyCode CurrencyCode { get; set; }
}
