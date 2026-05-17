using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class BankAccountEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListBankAccounts_returns_ok()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/bank-accounts", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var bankAccounts = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<BankAccountDto>
        >();
        await Assert.That(bankAccounts).IsNotNull();
    }

    [Test]
    public async Task CreateBankAccount_for_an_account_round_trips()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-1");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL91ABNA0417164300",
            AccountNumber: null,
            Bic: "ABNANL2A",
            BankName: "ABN AMRO",
            AccountHolderName: "Me",
            CurrencyCode: "EUR",
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<BankAccountDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Iban).IsEqualTo("NL91ABNA0417164300");
        await Assert.That(created.AccountId).IsEqualTo(account.Id);
        await Assert.That(created.CounterpartyId).IsNull();

        using var getResponse = await client.GetAsync(
            new Uri($"/bank-accounts/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateBankAccount_for_a_counterparty_round_trips()
    {
        using var client = Factory.CreateClient();
        var counterparty = await CreateCounterpartyAsync(client, "Albert Heijn (BA)");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL31RABO0123456789",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: null,
            CounterpartyId: counterparty.Id
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        await Assert.That(created!.CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(created.AccountId).IsNull();
    }

    [Test]
    public async Task CreateBankAccount_with_neither_owner_returns_422()
    {
        using var client = Factory.CreateClient();

        var request = new CreateBankAccountRequestDto(
            Iban: "NL12RABO0100000001",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: null,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateBankAccount_with_both_owners_returns_422()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-Both");
        var counterparty = await CreateCounterpartyAsync(client, "Counterparty-Both");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL12RABO0100000002",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: counterparty.Id
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateBankAccount_missing_iban_and_account_number_returns_422()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-NoIdent");

        var request = new CreateBankAccountRequestDto(
            Iban: null,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateBankAccount_only_account_number_succeeds()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "ING-Savings");

        var request = new CreateBankAccountRequestDto(
            Iban: null,
            AccountNumber: "777888999",
            Bic: null,
            BankName: "ING",
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateBankAccount_duplicate_iban_returns_409()
    {
        using var client = Factory.CreateClient();
        var firstAccount = await CreateAccountAsync(client, "Dup-Iban-1");
        var secondAccount = await CreateAccountAsync(client, "Dup-Iban-2");

        var first = new CreateBankAccountRequestDto(
            Iban: "NL68INGB0001234567",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: firstAccount.Id,
            CounterpartyId: null
        );
        using var firstResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = first with { AccountId = secondAccount.Id };
        using var duplicateResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            duplicate
        );

        await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateBankAccount_duplicate_account_returns_409()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Dup-AccountId");

        var first = new CreateBankAccountRequestDto(
            Iban: "NL01ABNA0000000001",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var firstResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = first with { Iban = "NL01ABNA0000000002" };
        using var duplicateResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            duplicate
        );

        await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateBankAccount_unknown_account_returns_404()
    {
        using var client = Factory.CreateClient();

        var request = new CreateBankAccountRequestDto(
            Iban: "NL01ABNA0000099999",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: Guid.NewGuid(),
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateBankAccount_invalid_iban_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Invalid-Iban");

        var request = new CreateBankAccountRequestDto(
            Iban: "not-an-iban",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetBankAccount_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/bank-accounts/{Guid.NewGuid()}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateBankAccount_changes_metadata()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "To-Update");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL44RABO0987654321",
            AccountNumber: null,
            Bic: null,
            BankName: "OldBank",
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankAccountDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/bank-accounts/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/bankName", "NewBank")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<BankAccountDto>();
        await Assert.That(updated!.BankName).IsEqualTo("NewBank");
    }

    [Test]
    public async Task UpdateBankAccount_can_clear_optional_fields()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "To-Clear");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL77RABO0700070007",
            AccountNumber: "1234567",
            Bic: "RABONL2U",
            BankName: "OldBank",
            AccountHolderName: "Old Holder",
            CurrencyCode: "EUR",
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankAccountDto>();

        // Iban stays set so the IBAN-or-AccountNumber invariant holds; everything else
        // becomes genuinely null via JSON Patch replace-to-null.
        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/bank-accounts/{created!.Id}", UriKind.Relative),
            [
                JsonPatchHelpers.Replace("/bic", null),
                JsonPatchHelpers.Replace("/bankName", null),
                JsonPatchHelpers.Replace("/accountHolderName", null),
                JsonPatchHelpers.Replace("/currencyCode", null),
                JsonPatchHelpers.Replace("/accountNumber", null),
            ]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<BankAccountDto>();
        await Assert.That(updated!.Bic).IsNull();
        await Assert.That(updated.BankName).IsNull();
        await Assert.That(updated.AccountHolderName).IsNull();
        await Assert.That(updated.CurrencyCode).IsNull();
        await Assert.That(updated.AccountNumber).IsNull();
        await Assert.That(updated.Iban).IsEqualTo("NL77RABO0700070007");
    }

    [Test]
    public async Task UpdateBankAccount_clearing_last_identifier_returns_422()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Strip-Identifier");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL88RABO0800080008",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankAccountDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/bank-accounts/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/iban", null)]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task DeleteBankAccount_removes_the_row()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "To-Delete");

        var request = new CreateBankAccountRequestDto(
            Iban: "NL99RABO0500050005",
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: null,
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankAccountDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/bank-accounts/{created!.Id}", UriKind.Relative)
        );
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/bank-accounts/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<AccountDto> CreateAccountAsync(HttpClient client, string name)
    {
        var req = new CreateAccountRequestDto(name, "Asset", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task<CounterpartyDto> CreateCounterpartyAsync(
        HttpClient client,
        string name
    )
    {
        var req = new CreateCounterpartyRequestDto(name);
        using var response = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CounterpartyDto>();
        return dto!;
    }
}

internal sealed record BankAccountDto(
    Guid Id,
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    string? CurrencyCode,
    Guid? AccountId,
    Guid? CounterpartyId
);

internal sealed record CreateBankAccountRequestDto(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    string? CurrencyCode,
    Guid? AccountId,
    Guid? CounterpartyId
);
