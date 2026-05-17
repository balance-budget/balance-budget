using Balance.Data;
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

    public async Task<IReadOnlyList<CurrencyOutput>> ListAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .Currencies.OrderBy(c => c.Code)
            .Select(c => new CurrencyOutput(c.Code, c.Name, c.MinorUnitScale, c.Symbol))
            .ToListAsync(cancellationToken);

    public Task<CurrencyOutput?> GetAsync(CurrencyCode code, CancellationToken cancellationToken) =>
        _dbContext
            .Currencies.Where(c => c.Code == code)
            .Select(c => new CurrencyOutput(c.Code, c.Name, c.MinorUnitScale, c.Symbol))
            .FirstOrDefaultAsync(cancellationToken);
}
