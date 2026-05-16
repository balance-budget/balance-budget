using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountService
{
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken);

    Task<Account?> GetAsync(AccountId id, CancellationToken cancellationToken);

    Task<Account> CreateAsync(
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    Task<Account> UpdateAsync(
        AccountId id,
        string? name,
        AccountType? accountType,
        CurrencyCode? currencyCode,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(AccountId id, CancellationToken cancellationToken);
}
