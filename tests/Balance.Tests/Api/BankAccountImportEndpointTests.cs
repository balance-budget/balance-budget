using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class BankAccountImportEndpointTests : EndpointsTestsBase
{
    private const string Header =
        "\"Date\";\"Name / Description\";\"Account\";\"Counterparty\";\"Code\";"
        + "\"Debit/credit\";\"Amount (EUR)\";\"Transaction type\";\"Notifications\";"
        + "\"Resulting balance\";\"Tag\"";

    private const string RowEtosTemplate =
        "\"20260131\";\"Etos\";\"{0}\";\"\";\"BA\";\"Debit\";"
        + "\"72,30\";\"Payment terminal\";\"Card sequence no.: 001 31-01-2026 09:26\";\"1183,44\";\"\"";

    private const string RowCoolblueTemplate =
        "\"20260130\";\"Coolblue BV\";\"{0}\";\"NL22INGB3141592653\";\"GT\";"
        + "\"Credit\";\"59,95\";\"Online Banking\";\"Name: Coolblue BV\";\"1412,95\";\"\"";

    [Test]
    public async Task ImportStatement_happy_path_returns_ok_with_counts()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-Import-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);

        var csvBytes = BuildCsvBytes(iban, RowEtosTemplate, RowCoolblueTemplate);
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var response = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Imported).IsEqualTo(2);
        await Assert.That(result.SkippedAsDuplicate).IsEqualTo(0);
    }

    [Test]
    public async Task ImportStatement_idempotent_re_upload_returns_skip_counts()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-Idemp-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);
        var url = new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative);
        var csvBytes = BuildCsvBytes(iban, RowEtosTemplate, RowCoolblueTemplate);

        using (var firstFile = new ByteArrayContent(csvBytes))
        {
            firstFile.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            using var firstMultipart = new MultipartFormDataContent
            {
                { firstFile, "file", "statement.csv" },
            };
            using var first = await client.PostAsync(url, firstMultipart);
            await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        using var secondFile = new ByteArrayContent(csvBytes);
        secondFile.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var secondMultipart = new MultipartFormDataContent
        {
            { secondFile, "file", "statement.csv" },
        };
        using var second = await client.PostAsync(url, secondMultipart);

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await second.Content.ReadFromJsonAsync<ImportResultDto>();
        await Assert.That(result!.Imported).IsEqualTo(0);
        await Assert.That(result.SkippedAsDuplicate).IsEqualTo(2);
    }

    [Test]
    public async Task ImportStatement_unknown_bank_account_returns_404()
    {
        using var client = Factory.CreateClient();
        var csvBytes = BuildCsvBytes("NL69INGB0123456789", RowEtosTemplate);
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var response = await client.PostAsync(
            new Uri($"/api/bank-accounts/{Guid.NewGuid()}/imports", UriKind.Relative),
            multipart
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ImportStatement_csv_account_mismatch_returns_422()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-Mismatch-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);

        var csvBytes = BuildCsvBytes("NL11RABO9999999999", RowEtosTemplate);
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var response = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ImportStatement_counterparty_bank_account_returns_422()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL01INGB{NextDigits(10)}";
        var counterparty = await CreateCounterpartyAsync(client, $"CP-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForCounterpartyAsync(
            client,
            iban,
            counterparty.Id
        );

        var csvBytes = BuildCsvBytes(iban, RowEtosTemplate);
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var response = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ImportStatement_empty_file_returns_400()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-Empty-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);

        using var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent { { fileContent, "file", "empty.csv" } };

        using var response = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static byte[] BuildCsvBytes(string iban, params string[] rowTemplates)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var template in rowTemplates)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, template, iban));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static async Task<AccountDto> CreateAccountAsync(HttpClient client, string name)
    {
        var req = new CreateAccountRequestDto(name, "Asset", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
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
            new Uri("/api/counterparties", UriKind.Relative),
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
            new Uri("/api/bank-accounts", UriKind.Relative),
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
            CurrencyCode: null,
            AccountId: null,
            CounterpartyId: counterpartyId
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }

    private static string NextDigits(int length)
    {
        var digits = new char[length];
        for (var i = 0; i < length; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        return new string(digits);
    }
}

internal sealed record ImportResultDto(int Imported, int SkippedAsDuplicate);
