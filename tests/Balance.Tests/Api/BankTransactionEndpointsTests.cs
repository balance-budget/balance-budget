using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class BankTransactionEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListBankTransactions_returns_ok()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/bank-transactions", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<BankTransactionDto>>();
        await Assert.That(list).IsNotNull();
    }

    [Test]
    public async Task CreateBankTransaction_for_owned_bank_account_round_trips()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-1");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL12RABO0BTX00001",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: -12345L,
            CurrencyCode: "EUR"
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.BankAccountId).IsEqualTo(bankAccount.Id);
        await Assert.That(created.Money.Amount).IsEqualTo(-12345L);
        await Assert.That(created.Money.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(created.BookingDate).IsEqualTo(new DateOnly(2026, 5, 17));

        using var getResponse = await client.GetAsync(
            new Uri($"/bank-transactions/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(fetched!.Money.Amount).IsEqualTo(-12345L);
        await Assert.That(fetched.Money.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task CreateBankTransaction_on_counterparty_bank_account_returns_422()
    {
        using var client = Factory.CreateClient();
        var counterparty = await CreateCounterpartyAsync(client, "Counterparty-BTX-1");
        var bankAccount = await CreateBankAccountForCounterpartyAsync(
            client,
            iban: "NL31RABO0BTX00002",
            counterpartyId: counterparty.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 1000L,
            CurrencyCode: "EUR"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateBankTransaction_unknown_bank_account_returns_404()
    {
        using var client = Factory.CreateClient();

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: Guid.NewGuid(),
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 250L,
            CurrencyCode: "EUR"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateBankTransaction_unknown_currency_returns_404()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Currency");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL01RABO0BTX00003",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 250L,
            CurrencyCode: "XYZ"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateBankTransaction_zero_amount_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Zero");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL02RABO0BTX00004",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 0L,
            CurrencyCode: "EUR"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetBankTransaction_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/bank-transactions/{Guid.NewGuid()}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteBankTransaction_removes_the_row()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Delete");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL03RABO0BTX00005",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 1L,
            CurrencyCode: "EUR"
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/bank-transactions", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankTransactionDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/bank-transactions/{created!.Id}", UriKind.Relative)
        );
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/bank-transactions/{created.Id}", UriKind.Relative)
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

    private static async Task<BankAccountDto> CreateBankAccountForAccountAsync(
        HttpClient client,
        string iban,
        Guid accountId
    )
    {
        var req = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: accountId,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }

    private static async Task<BankAccountDto> CreateBankAccountForCounterpartyAsync(
        HttpClient client,
        string iban,
        Guid counterpartyId
    )
    {
        var req = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: null,
            CounterpartyId: counterpartyId
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }
}

internal sealed record BankTransactionDto(
    Guid Id,
    Guid BankAccountId,
    DateOnly BookingDate,
    MoneyDto Money,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal sealed record MoneyDto(long Amount, string CurrencyCode);

internal sealed record CreateBankTransactionRequestDto(
    Guid BankAccountId,
    DateOnly BookingDate,
    long Amount,
    string CurrencyCode
);
