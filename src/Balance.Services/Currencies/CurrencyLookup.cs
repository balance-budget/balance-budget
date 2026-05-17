using System.Collections.Concurrent;
using Balance.Data;
using Balance.Data.Currencies;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Services.Currencies;

internal sealed class CurrencyLookup : ICurrencyLookup
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lock _gate = new();
    private ConcurrentDictionary<CurrencyCode, Currency>? _cache;

    public CurrencyLookup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Currency GetByCode(CurrencyCode code) =>
        TryGetByCode(code)
        ?? throw new DomainException(
            DomainExceptionKind.NotFound,
            $"Currency '{code.Value}' is not defined."
        );

    public Currency? TryGetByCode(CurrencyCode code)
    {
        EnsureWarmed();
        return _cache!.GetValueOrDefault(code);
    }

    internal async Task WarmAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var currencies = await dbContext.Currencies.AsNoTracking().ToListAsync(cancellationToken);

        var snapshot = new ConcurrentDictionary<CurrencyCode, Currency>(
            currencies.Select(c => new KeyValuePair<CurrencyCode, Currency>(c.Code, c))
        );

        lock (_gate)
        {
            _cache = snapshot;
        }
    }

    private void EnsureWarmed()
    {
        if (_cache is not null)
        {
            return;
        }

        // Defensive path: if Get/TryGet is called before the hosted warmer has run,
        // synchronously warm the cache. Avoids null-ref crashes in tests/console contexts.
        WarmAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
