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

        using var response = await client.GetAsync(new Uri("/api/currencies", UriKind.Relative));

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

        using var response = await client.GetAsync(
            new Uri("/api/currencies/EUR", UriKind.Relative)
        );

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

        using var response = await client.GetAsync(
            new Uri("/api/currencies/XYZ", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateCurrency_round_trips()
    {
        using var client = Factory.CreateClient();

        var request = new CreateCurrencyRequestDto("SEK", "Swedish Krona", 2, "kr");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Code).IsEqualTo("SEK");
        await Assert.That(created.Symbol).IsEqualTo("kr");

        using var getResponse = await client.GetAsync(
            new Uri("/api/currencies/SEK", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CurrencyDto>();
        await Assert.That(fetched!.Code).IsEqualTo("SEK");
        await Assert.That(fetched.Name).IsEqualTo("Swedish Krona");
    }

    [Test]
    public async Task CreateCurrency_duplicate_code_returns_409()
    {
        using var client = Factory.CreateClient();

        var duplicate = new CreateCurrencyRequestDto("EUR", "Whatever", 2, null);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            duplicate
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateCurrency_invalid_request_returns_400()
    {
        using var client = Factory.CreateClient();

        var invalid = new CreateCurrencyRequestDto("", "", -1, null);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            invalid
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateCurrency_changes_name_and_symbol()
    {
        using var client = Factory.CreateClient();

        var create = new CreateCurrencyRequestDto("NOK", "Krone", 2, "kr");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            create
        );
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var update = new UpdateCurrencyRequestDto("Norwegian Krone", "NOK");
        using var updateResponse = await client.PatchAsJsonAsync(
            new Uri("/api/currencies/NOK", UriKind.Relative),
            update
        );

        await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CurrencyDto>();
        await Assert.That(updated!.Name).IsEqualTo("Norwegian Krone");
        await Assert.That(updated.Symbol).IsEqualTo("NOK");
    }

    [Test]
    public async Task UpdateCurrency_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        var update = new UpdateCurrencyRequestDto("Whatever", null);
        using var response = await client.PatchAsJsonAsync(
            new Uri("/api/currencies/XYZ", UriKind.Relative),
            update
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteCurrency_removes_unreferenced_currency()
    {
        using var client = Factory.CreateClient();

        var create = new CreateCurrencyRequestDto("NZD", "New Zealand Dollar", 2, "$");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            create
        );
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var deleteResponse = await client.DeleteAsync(
            new Uri("/api/currencies/NZD", UriKind.Relative)
        );

        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri("/api/currencies/NZD", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteCurrency_returns_409_when_referenced()
    {
        using var client = Factory.CreateClient();

        // EUR is seeded and referenced by the Opening Balances account.
        using var deleteResponse = await client.DeleteAsync(
            new Uri("/api/currencies/EUR", UriKind.Relative)
        );

        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateAccount_with_user_added_currency_succeeds()
    {
        using var client = Factory.CreateClient();

        // Hit GET first to populate the read cache (cache-miss → cache-hit sequence).
        using var listBefore = await client.GetAsync(new Uri("/api/currencies", UriKind.Relative));
        await Assert.That(listBefore.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // User adds a new currency at runtime.
        var newCurrency = new CreateCurrencyRequestDto("AUD", "Australian Dollar", 2, "$");
        using var createCurrencyResponse = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            newCurrency
        );
        await Assert.That(createCurrencyResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        // The new currency must be immediately usable by Account creation
        // (cache must have been invalidated on write).
        var account = new CreateAccountRequestDto("Aussie Savings", "Asset", "AUD");
        using var createAccountResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            account
        );

        await Assert.That(createAccountResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }
}

internal sealed record CurrencyDto(string Code, string Name, int MinorUnitScale, string? Symbol);

internal sealed record CreateCurrencyRequestDto(
    string Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
);

internal sealed record UpdateCurrencyRequestDto(string? Name, string? Symbol);
