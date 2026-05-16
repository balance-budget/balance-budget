using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Currencies;

internal sealed class CurrencyService : ICurrencyService
{
    private readonly BalanceDbContext _dbContext;

    public CurrencyService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Currency>> ListAsync(CancellationToken cancellationToken) =>
        await _dbContext
            .Currencies.AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

    public Task<Currency?> GetAsync(CurrencyCode code, CancellationToken cancellationToken) =>
        _dbContext
            .Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
}
