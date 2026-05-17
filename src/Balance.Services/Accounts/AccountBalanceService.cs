using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
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

    public async Task<Money?> GetBalanceAsync(AccountId id, CancellationToken cancellationToken)
    {
        var account = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.AccountType, a.CurrencyCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return null;
        }

        var sum = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => l.AccountId == id)
            .SumAsync(l => l.Amount, cancellationToken);

        var signed = IsCreditNormal(account.AccountType) ? checked(-sum) : sum;
        return new Money(signed, account.CurrencyCode);
    }

    private static bool IsCreditNormal(AccountType accountType) =>
        accountType switch
        {
            AccountType.Asset or AccountType.Expense => false,
            AccountType.Liability or AccountType.Equity or AccountType.Income => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(accountType),
                accountType,
                "Unknown AccountType."
            ),
        };
}
