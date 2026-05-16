using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class AccountEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListAccounts_returns_seeded_opening_balances()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/accounts", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<IReadOnlyList<AccountDto>>();
        await Assert.That(accounts).IsNotNull();
        await Assert.That(accounts!.Select(a => a.Name)).Contains("Opening Balances");

        var openingBalances = accounts.Single(a => a.Name == "Opening Balances");
        await Assert.That(openingBalances.AccountType).IsEqualTo("Equity");
        await Assert.That(openingBalances.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task CreateAccount_round_trips()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("ABN AMRO Checking", "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Name).IsEqualTo("ABN AMRO Checking");
        await Assert.That(created.AccountType).IsEqualTo("Asset");
        await Assert.That(created.CurrencyCode).IsEqualTo("EUR");

        using var getResponse = await client.GetAsync(
            new Uri($"/accounts/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(fetched!.Id).IsEqualTo(created.Id);
        await Assert.That(fetched.Name).IsEqualTo("ABN AMRO Checking");
    }

    [Test]
    public async Task CreateAccount_duplicate_name_case_insensitive_returns_409()
    {
        using var client = Factory.CreateClient();

        var first = new CreateAccountRequestDto("Groceries", "Expense", "EUR");
        using var firstResponse = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = new CreateAccountRequestDto("groceries", "Expense", "EUR");
        using var duplicateResponse = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            duplicate
        );

        await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GetAccount_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        var unknownId = Guid.NewGuid();
        using var response = await client.GetAsync(
            new Uri($"/accounts/{unknownId}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateAccount_unknown_currency_returns_404()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Mystery", "Asset", "XYZ");
        using var response = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateAccount_renames()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Old Name", "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        var update = new UpdateAccountRequestDto("New Name", null, null);
        using var patchResponse = await client.PatchAsJsonAsync(
            new Uri($"/accounts/{created!.Id}", UriKind.Relative),
            update
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(updated!.Name).IsEqualTo("New Name");
    }

    [Test]
    public async Task DeleteAccount_removes_the_row()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Temporary", "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/accounts/{created!.Id}", UriKind.Relative)
        );

        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/accounts/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by System.Text.Json deserialization."
)]
internal sealed record AccountDto(Guid Id, string Name, string AccountType, string CurrencyCode);

internal sealed record CreateAccountRequestDto(
    string Name,
    string AccountType,
    string CurrencyCode
);

internal sealed record UpdateAccountRequestDto(
    string? Name,
    string? AccountType,
    string? CurrencyCode
);
