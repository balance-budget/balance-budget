using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Balance.Services.Currencies;

internal sealed class CurrencyService : ICurrencyService
{
    internal const string ListCacheKey = "currency:list";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly BalanceDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public CurrencyService(BalanceDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<CurrencyOutput>> ListAsync(CancellationToken cancellationToken)
    {
        var cached = await _cache.GetOrCreateAsync(
            ListCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                var currencies = await _dbContext
                    .Currencies.AsNoTracking()
                    .OrderBy(c => c.Code)
                    .ToListAsync(cancellationToken);
                IReadOnlyList<CurrencyOutput> outputs =
                [
                    .. currencies.Select(CurrencyOutput.FromEntity),
                ];
                return outputs;
            }
        );
        return cached ?? [];
    }

    public async Task<CurrencyOutput?> GetAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    )
    {
        return await _cache.GetOrCreateAsync(
            CacheKey(code),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                var currency = await _dbContext
                    .Currencies.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
                return currency is null ? null : CurrencyOutput.FromEntity(currency);
            }
        );
    }

    public async Task<CurrencyOutput> CreateAsync(
        CreateCurrencyInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var trimmedName = input.Name.Trim();
        var trimmedSymbol = string.IsNullOrWhiteSpace(input.Symbol) ? null : input.Symbol.Trim();

        var exists = await _dbContext.Currencies.AnyAsync(
            c => c.Code == input.Code,
            cancellationToken
        );
        if (exists)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"Currency '{input.Code.Value}' already exists."
            );
        }

        var currency = new Currency
        {
            Code = input.Code,
            Name = trimmedName,
            MinorUnitScale = input.MinorUnitScale,
            Symbol = trimmedSymbol,
        };
        _dbContext.Currencies.Add(currency);
        await _dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache(input.Code);
        return CurrencyOutput.FromEntity(currency);
    }

    public async Task<CurrencyOutput> UpdateAsync(
        CurrencyCode code,
        UpdateCurrencyInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var currency =
            await _dbContext.Currencies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{code.Value}' not found."
            );

        if (input.Name is not null)
        {
            var trimmed = input.Name.Trim();
            if (trimmed.Length == 0)
            {
                throw new DomainException(
                    DomainExceptionKind.Validation,
                    "Currency name cannot be empty."
                );
            }
            currency.Name = trimmed;
        }

        if (input.Symbol is not null)
        {
            var trimmed = input.Symbol.Trim();
            currency.Symbol = trimmed.Length == 0 ? null : trimmed;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache(code);
        return CurrencyOutput.FromEntity(currency);
    }

    public async Task DeleteAsync(CurrencyCode code, CancellationToken cancellationToken)
    {
        var currency =
            await _dbContext.Currencies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{code.Value}' not found."
            );

        _dbContext.Currencies.Remove(currency);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"Currency '{code.Value}' is referenced by other records and cannot be deleted.",
                ex
            );
        }

        InvalidateCache(code);
    }

    internal static string CacheKey(CurrencyCode code) => $"currency:{code.Value}";

    private void InvalidateCache(CurrencyCode code)
    {
        _cache.Remove(CacheKey(code));
        _cache.Remove(ListCacheKey);
    }
}
