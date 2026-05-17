using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountBalanceService
{
    Task<Money?> GetBalanceAsync(AccountId id, CancellationToken cancellationToken);
}
