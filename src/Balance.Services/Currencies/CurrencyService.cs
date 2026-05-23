using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Balance.Services.Currencies;

internal sealed class CurrencyService : ICurrencyService
{
    private const string ListCacheKey = "currency:list";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

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

    public async Task<Result<CurrencyOutput>> GetAsync(
        CurrencyCode code,
        CancellationToken cancellationToken
    )
    {
        var output = await _cache.GetOrCreateAsync(
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
        return output is null ? new NotFoundError("Currency", code.Value) : output;
    }

    public async Task<Result<CurrencyOutput>> CreateAsync(
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
            return new ConflictError(
                ErrorCodes.CurrencyExists,
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
        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } err })
        {
            return err;
        }

        InvalidateCache(input.Code);
        return CurrencyOutput.FromEntity(currency);
    }

    public async Task<Result<CurrencyOutput>> UpdateAsync(
        CurrencyCode code,
        UpdateCurrencyInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var currency = await _dbContext.Currencies.FirstOrDefaultAsync(
            c => c.Code == code,
            cancellationToken
        );
        if (currency is null)
        {
            return new NotFoundError("Currency", code.Value);
        }

        if (input.Name is not null)
        {
            var trimmed = input.Name.Trim();
            if (trimmed.Length == 0)
            {
                return new InvariantError(
                    ErrorCodes.CurrencyNameEmpty,
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

        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } err })
        {
            return err;
        }

        InvalidateCache(code);
        return CurrencyOutput.FromEntity(currency);
    }

    public async Task<Result> DeleteAsync(CurrencyCode code, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .Currencies.Where(c => c.Code == code)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("Currency", code.Value);

        InvalidateCache(code);

        return Result.Success;
    }

    internal static string CacheKey(CurrencyCode code) => $"currency:{code.Value}";

    private void InvalidateCache(CurrencyCode code)
    {
        _cache.Remove(CacheKey(code));
        _cache.Remove(ListCacheKey);
    }
}
