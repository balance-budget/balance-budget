using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class CurrencyEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListCurrencies_returns_seeded_currencies()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/currencies", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var currencies = await response.Content.ReadFromJsonAsync<IReadOnlyList<CurrencyDto>>();
        await Assert.That(currencies).IsNotNull();
        await Assert.That(currencies!.Select(c => c.Code)).Contains("EUR");
        await Assert.That(currencies.Select(c => c.Code)).Contains("USD");
        await Assert.That(currencies.Select(c => c.Code)).Contains("JPY");
        await Assert.That(currencies.Select(c => c.Code)).Contains("BTC");

        var jpy = currencies.Single(c => c.Code == "JPY");
        await Assert.That(jpy.MinorUnitScale).IsEqualTo(0);

        var btc = currencies.Single(c => c.Code == "BTC");
        await Assert.That(btc.MinorUnitScale).IsEqualTo(8);
    }

    [Test]
    public async Task GetCurrency_returns_seeded_currency()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/currencies/EUR", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        await Assert.That(currency).IsNotNull();
        await Assert.That(currency!.Code).IsEqualTo("EUR");
        await Assert.That(currency.MinorUnitScale).IsEqualTo(2);
        await Assert.That(currency.Symbol).IsEqualTo("€");
    }

    [Test]
    public async Task GetCurrency_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/currencies/XYZ", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by System.Text.Json deserialization."
)]
internal sealed record CurrencyDto(string Code, string Name, int MinorUnitScale, string? Symbol);
