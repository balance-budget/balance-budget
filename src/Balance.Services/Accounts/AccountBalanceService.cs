using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Accounts;

internal sealed class AccountBalanceService : IAccountBalanceService
{
    private readonly BalanceDbContext _dbContext;

    public AccountBalanceService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Money>> GetBalanceAsync(
        AccountId id,
        CancellationToken cancellationToken
    )
    {
        var account = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.AccountType, a.CurrencyCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return new NotFoundError("Account", id.Value.ToString());
        }

        var sum = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => l.AccountId == id)
            .SumAsync(l => l.Amount, cancellationToken);

        return AccountSignConvention.ToBalance(account.AccountType, sum, account.CurrencyCode);
    }
}
