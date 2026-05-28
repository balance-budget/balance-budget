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

        using var response = await client.GetAsync(new Uri("/api/accounts", UriKind.Relative));

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
            new Uri("/api/accounts", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Name).IsEqualTo("ABN AMRO Checking");
        await Assert.That(created.AccountType).IsEqualTo("Asset");
        await Assert.That(created.CurrencyCode).IsEqualTo("EUR");

        using var getResponse = await client.GetAsync(
            new Uri($"/api/accounts/{created.Id}", UriKind.Relative)
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
            new Uri("/api/accounts", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = new CreateAccountRequestDto("groceries", "Expense", "EUR");
        using var duplicateResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
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
            new Uri($"/api/accounts/{unknownId}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateAccount_unknown_currency_returns_404()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Mystery", "Asset", "XYZ");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
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
            new Uri("/api/accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/accounts/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/name", "New Name")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(updated!.Name).IsEqualTo("New Name");
    }

    [Test]
    public async Task UpdateAccount_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/accounts/{Guid.NewGuid()}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/name", "Anything")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateAccount_with_invalid_path_returns_400()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Patch-Errors", "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/accounts/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/doesNotExist", "x")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListAccounts_includes_zero_balance_for_account_with_no_lines()
    {
        using var client = Factory.CreateClient();
        var name = $"Empty-List-{Guid.NewGuid():N}";
        var request = new CreateAccountRequestDto(name, "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            request
        );
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var listResponse = await client.GetAsync(new Uri("/api/accounts", UriKind.Relative));
        var accounts = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<AccountDto>>();
        var account = accounts!.Single(a => a.Name == name);

        await Assert.That(account.Balance).IsNotNull();
        await Assert.That(account.Balance!.Amount).IsEqualTo(0L);
        await Assert.That(account.Balance.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(account.BankAccount).IsNull();
    }

    [Test]
    public async Task ListAccounts_signs_balance_per_account_type()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(
            client,
            $"Groceries-List-{Guid.NewGuid():N}",
            "Expense"
        );
        var salary = await CreateAccountAsync(client, $"Salary-List-{Guid.NewGuid():N}", "Income");
        var checking = await CreateAccountAsync(
            client,
            $"Checking-List-{Guid.NewGuid():N}",
            "Asset"
        );

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(checking.Id, 250_000, null),
                new CreateJournalLineRequestDto(salary.Id, -250_000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 4_000, null),
                new CreateJournalLineRequestDto(checking.Id, -4_000, null),
            ]
        );

        using var listResponse = await client.GetAsync(new Uri("/api/accounts", UriKind.Relative));
        var accounts = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<AccountDto>>();
        var byId = accounts!.ToDictionary(a => a.Id);

        await Assert.That(byId[groceries.Id].Balance!.Amount).IsEqualTo(4_000L);
        await Assert.That(byId[checking.Id].Balance!.Amount).IsEqualTo(246_000L);
        await Assert.That(byId[salary.Id].Balance!.Amount).IsEqualTo(250_000L);
    }

    [Test]
    public async Task ListAccounts_includes_bank_account_summary_when_linked()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, $"Linked-{Guid.NewGuid():N}", "Asset");
        var iban = UniqueIban();
        var bankRequest = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: "ABNANL2A",
            BankName: "ABN AMRO",
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: account.Id,
            CounterpartyId: null
        );
        using var createBank = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            bankRequest
        );
        await Assert.That(createBank.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var listResponse = await client.GetAsync(new Uri("/api/accounts", UriKind.Relative));
        var accounts = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<AccountDto>>();
        var found = accounts!.Single(a => a.Id == account.Id);

        await Assert.That(found.BankAccount).IsNotNull();
        await Assert.That(found.BankAccount!.Iban).IsEqualTo(iban);
        await Assert.That(found.BankAccount.AccountNumber).IsNull();
        await Assert.That(found.BankAccount.Bic).IsEqualTo("ABNANL2A");
        await Assert.That(found.BankAccount.BankName).IsEqualTo("ABN AMRO");
    }

    [Test]
    public async Task GetAccount_returns_enriched_balance_and_bank_account_summary()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, $"Single-{Guid.NewGuid():N}", "Asset");
        var accountNumber = $"AN{Guid.NewGuid():N}";
        var bankRequest = new CreateBankAccountRequestDto(
            Iban: null,
            AccountNumber: accountNumber,
            Bic: null,
            BankName: "Bunq",
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: account.Id,
            CounterpartyId: null,
            Type: "Savings"
        );
        using var createBank = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            bankRequest
        );
        await Assert.That(createBank.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{account.Id}", UriKind.Relative)
        );
        var fetched = await response.Content.ReadFromJsonAsync<AccountDto>();

        await Assert.That(fetched!.Balance!.Amount).IsEqualTo(0L);
        await Assert.That(fetched.BankAccount).IsNotNull();
        await Assert.That(fetched.BankAccount!.Iban).IsNull();
        await Assert.That(fetched.BankAccount.AccountNumber).IsEqualTo(accountNumber);
        await Assert.That(fetched.BankAccount.BankName).IsEqualTo("Bunq");
    }

    [Test]
    public async Task DeleteAccount_removes_the_row()
    {
        using var client = Factory.CreateClient();

        var request = new CreateAccountRequestDto("Temporary", "Asset", "EUR");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/api/accounts/{created!.Id}", UriKind.Relative)
        );

        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/api/accounts/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // BankAccounts.Iban is uniquely indexed and the integration-test session shares one
    // SQLite database across test classes, so any hardcoded IBAN collides with the first
    // class that boots. Mint a fresh IBAN per test instead.
    private static string UniqueIban() =>
        ("NL91TEST" + Guid.NewGuid().ToString("N").ToUpperInvariant())[..34];

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode = "EUR"
    )
    {
        var req = new CreateAccountRequestDto(name, accountType, currencyCode);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            CounterpartyId: null,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }
}

internal sealed record AccountDto(
    Guid Id,
    string Name,
    string AccountType,
    string CurrencyCode,
    MoneyDto? Balance = null,
    BankAccountSummaryDto? BankAccount = null
);

internal sealed record BankAccountSummaryDto(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName
);

internal sealed record CreateAccountRequestDto(
    string Name,
    string AccountType,
    string CurrencyCode
);
