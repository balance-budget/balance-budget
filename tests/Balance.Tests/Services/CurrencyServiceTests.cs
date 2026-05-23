using Balance.Data;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Currencies;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class CurrencyServiceTests : EndpointsTestsBase
{
    [Test]
    public async Task GetAsync_caches_subsequent_reads(CancellationToken cancellationToken)
    {
        var code = UniqueCode("X");

        using (var scope = Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
            await service.CreateAsync(
                new CreateCurrencyInput(code, "Test", 2, null),
                cancellationToken
            );
        }

        // Prime the cache (cache miss → DB load → cache populated).
        using (var primeScope = Factory.Services.CreateScope())
        {
            var service = primeScope.ServiceProvider.GetRequiredService<ICurrencyService>();
            var primed = await service.GetAsync(code, cancellationToken);
            await Assert.That(primed.IsSuccess).IsTrue();
        }

        // Mutate the entity *directly* in the DB to detect cache hits — if the next read
        // returns the stale value we know it served from cache.
        using (var mutateScope = Factory.Services.CreateScope())
        {
            var dbContext = mutateScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
            await dbContext
                .Currencies.Where(c => c.Code == code)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, "Mutated"), cancellationToken);
        }

        using (var readScope = Factory.Services.CreateScope())
        {
            var service = readScope.ServiceProvider.GetRequiredService<ICurrencyService>();
            var stillCached = await service.GetAsync(code, cancellationToken);
            await Assert.That(stillCached.IsSuccess).IsTrue();
            await Assert.That(stillCached.Value!.Name).IsEqualTo("Test"); // served from cache
        }

        // Evict and re-read — the fresh value should now come back.
        var cache = Factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove(CurrencyService.CacheKey(code));

        using (var afterEvict = Factory.Services.CreateScope())
        {
            var service = afterEvict.ServiceProvider.GetRequiredService<ICurrencyService>();
            var fresh = await service.GetAsync(code, cancellationToken);
            await Assert.That(fresh.Value!.Name).IsEqualTo("Mutated");
        }
    }

    [Test]
    public async Task UpdateAsync_invalidates_cache(CancellationToken cancellationToken)
    {
        var code = UniqueCode("Y");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICurrencyService>();

        await service.CreateAsync(
            new CreateCurrencyInput(code, "Initial", 2, null),
            cancellationToken
        );
        var initial = await service.GetAsync(code, cancellationToken);
        await Assert.That(initial.Value!.Name).IsEqualTo("Initial");

        await service.UpdateAsync(
            code,
            new UpdateCurrencyInput("Updated", null),
            cancellationToken
        );

        // Without invalidation we would still see "Initial" — the previous GetAsync cached it.
        var afterUpdate = await service.GetAsync(code, cancellationToken);
        await Assert.That(afterUpdate.Value!.Name).IsEqualTo("Updated");
    }

    [Test]
    public async Task DeleteAsync_invalidates_cache(CancellationToken cancellationToken)
    {
        var code = UniqueCode("Z");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICurrencyService>();

        await service.CreateAsync(
            new CreateCurrencyInput(code, "Temp", 2, null),
            cancellationToken
        );
        var fetched = await service.GetAsync(code, cancellationToken);
        await Assert.That(fetched.IsSuccess).IsTrue();

        await service.DeleteAsync(code, cancellationToken);

        var afterDelete = await service.GetAsync(code, cancellationToken);
        await Assert.That(afterDelete.IsFailure).IsTrue();
        await Assert.That(afterDelete.Error).IsTypeOf<NotFoundError>();
    }

    [Test]
    public async Task ListAsync_returns_added_currency_after_invalidation(
        CancellationToken cancellationToken
    )
    {
        var code = UniqueCode("L");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICurrencyService>();

        // Prime list cache.
        var listBefore = await service.ListAsync(cancellationToken);
        await Assert.That(listBefore.Any(c => c.Code == code)).IsFalse();

        await service.CreateAsync(
            new CreateCurrencyInput(code, "Listed", 2, null),
            cancellationToken
        );

        var listAfter = await service.ListAsync(cancellationToken);
        await Assert.That(listAfter.Any(c => c.Code == code)).IsTrue();
    }

    private static CurrencyCode UniqueCode(string prefix) =>
        new(prefix + Guid.NewGuid().ToString("N").AsSpan(0, 6).ToString().ToUpperInvariant());
}
