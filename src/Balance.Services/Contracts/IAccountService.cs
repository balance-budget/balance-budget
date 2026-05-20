using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountService
{
    Task<IReadOnlyList<AccountOutput>> ListAsync(CancellationToken cancellationToken);

    Task<AccountOutput?> GetAsync(AccountId id, CancellationToken cancellationToken);

    Task<UpdateAccountInput?> GetSnapshotAsync(AccountId id, CancellationToken cancellationToken);

    Task<AccountOutput> CreateAsync(
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<AccountOutput> UpdateAsync(
        AccountId id,
        UpdateAccountInput input,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(AccountId id, CancellationToken cancellationToken);
}

public sealed record UpdateAccountInput
{
    public required string Name { get; set; }
    public required AccountType AccountType { get; set; }
    public required CurrencyCode CurrencyCode { get; set; }
}
