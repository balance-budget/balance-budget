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

        // Roll the balance up over the whole subtree (ADR-0019): a leaf sums only its own lines; a
        // non-postable account sums every descendant's. All descendants share this account's type and
        // currency (homogeneity), so the parent's sign convention applies to the combined raw sum.
        var nodes = await _dbContext
            .Accounts.AsNoTracking()
            .Select(a => new AccountNode(a.Id, a.ParentAccountId))
            .ToListAsync(cancellationToken);
        var ids = AccountTree.DescendantsAndSelf(nodes, id).ToList();

        var sum = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => ids.Contains(l.AccountId))
            .SumAsync(l => l.Amount, cancellationToken);

        return AccountSignConvention.ToBalance(account.AccountType, sum, account.CurrencyCode);
    }
}
