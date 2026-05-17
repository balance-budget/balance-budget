using Microsoft.Extensions.Hosting;

namespace Balance.Services.Currencies;

internal sealed class CurrencyLookupWarmer : IHostedService
{
    private readonly CurrencyLookup _lookup;

    public CurrencyLookupWarmer(CurrencyLookup lookup)
    {
        _lookup = lookup;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _lookup.WarmAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
