using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountService
{
    Task<IReadOnlyList<AccountOutput>> ListAsync(CancellationToken cancellationToken);

    Task<AccountOutput?> GetAsync(AccountId id, CancellationToken cancellationToken);

    Task<AccountOutput> CreateAsync(
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<AccountOutput> UpdateAsync(
        AccountId id,
        string? name,
        AccountType? accountType,
        CurrencyCode? currencyCode,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(AccountId id, CancellationToken cancellationToken);
}
