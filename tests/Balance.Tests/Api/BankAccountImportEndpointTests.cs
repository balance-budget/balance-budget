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

    private const string RowSepaDirectDebitTemplate =
        "\"20260129\";\"SPOTIFY BY ADYEN\";\"{0}\";\"NL48ABNA0502830042\";\"IC\";"
        + "\"Debit\";\"9,99\";\"SEPA Direct Debit\";"
        + "\"Naam: SPOTIFY BY ADYEN Omschrijving: SpotifyNL P0102A103D "
        + "IBAN: NL48ABNA0502830042 Kenmerk: SEPA-REF-001 "
        + "Machtiging ID: MNDT-12345 Incassant ID: NL74ZZZ546978200017 "
        + "Doorlopende incasso Valutadatum: 29-01-2026\";"
        + "\"1352,96\";\"\"";

    private const string RowForeignCurrencyTemplate =
        "\"20260128\";\"ATM Belarus\";\"{0}\";\"\";\"GM\";\"Debit\";"
        + "\"40,97\";\"Cash machine\";"
        + "\"Pasvolgnr: 008 28-01-2026 14:20 Transactie: Q71243 Term: ATM12713 "
        + "Valuta: 100,00 BYN Koers: 2,4406776 Opslag: 0,45 EUR Kosten: 2,25 EUR "
        + "Valutadatum: 28-01-2026\";\"1311,99\";\"\"";

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
    public async Task ImportStatement_populates_promoted_SEPA_and_FX_columns_and_ImporterKey()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-Metadata-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);

        var csvBytes = BuildCsvBytes(
            iban,
            RowEtosTemplate,
            RowSepaDirectDebitTemplate,
            RowForeignCurrencyTemplate
        );
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var importResponse = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );
        await Assert.That(importResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var rows = new List<BankTransactionDto>();
        for (var skip = 0; ; skip += 200)
        {
            using var listResponse = await client.GetAsync(
                new Uri($"/api/bank-transactions?skip={skip}&take=200&filter=All", UriKind.Relative)
            );
            await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var page = await listResponse.Content.ReadPagedItemsAsync<BankTransactionDto>();
            if (page.Count == 0)
                break;
            rows.AddRange(page.Where(r => r.BankAccountId == bankAccount.Id));
            if (page.Count < 200)
                break;
        }

        var sepa = rows.Single(r => r.Description == "SpotifyNL P0102A103D");
        await Assert.That(sepa.ValueDate).IsEqualTo(new DateOnly(2026, 1, 29));
        await Assert.That(sepa.Reference).IsEqualTo("SEPA-REF-001");
        await Assert.That(sepa.MandateId).IsEqualTo("MNDT-12345");
        await Assert.That(sepa.SepaCreditorId).IsEqualTo("NL74ZZZ546978200017");
        await Assert.That(sepa.ForeignAmount).IsNull();
        await Assert.That(sepa.ForeignCurrencyCode).IsNull();
        await Assert.That(sepa.ExchangeRate).IsNull();
        await Assert.That(sepa.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");

        var fx = rows.Single(r => r.Description == "ATM Belarus");
        await Assert.That(fx.ForeignAmount).IsEqualTo(10000L);
        await Assert.That(fx.ForeignCurrencyCode).IsEqualTo("BYN");
        await Assert.That(fx.ExchangeRate).IsEqualTo(2.4406776m);
        await Assert.That(fx.ValueDate).IsEqualTo(new DateOnly(2026, 1, 28));
        await Assert.That(fx.MandateId).IsNull();
        await Assert.That(fx.SepaCreditorId).IsNull();
        await Assert.That(fx.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");

        var etos = rows.Single(r => r.Description == "Etos");
        // Etos row has no SEPA / FX note fields — only the card-sequence date.
        await Assert.That(etos.Reference).IsNull();
        await Assert.That(etos.MandateId).IsNull();
        await Assert.That(etos.SepaCreditorId).IsNull();
        await Assert.That(etos.ForeignAmount).IsNull();
        await Assert.That(etos.ForeignCurrencyCode).IsNull();
        await Assert.That(etos.ExchangeRate).IsNull();
        await Assert.That(etos.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
    }

    [Test]
    public async Task ImportStatement_populates_metadata_entries_exposed_via_detail_endpoint()
    {
        using var client = Factory.CreateClient();
        var iban = $"NL69INGB{NextDigits(10)}";
        var account = await CreateAccountAsync(client, $"Checking-MetaTable-{Guid.NewGuid():N}");
        var bankAccount = await CreateBankAccountForAccountAsync(client, iban, account.Id);

        var csvBytes = BuildCsvBytes(iban, RowSepaDirectDebitTemplate, RowForeignCurrencyTemplate);
        using var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent
        {
            { fileContent, "file", "statement.csv" },
        };

        using var importResponse = await client.PostAsync(
            new Uri($"/api/bank-accounts/{bankAccount.Id}/imports", UriKind.Relative),
            multipart
        );
        await Assert.That(importResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var listResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions?skip=0&take=200&filter=All", UriKind.Relative)
        );
        var listRows = await listResponse.Content.ReadPagedItemsAsync<BankTransactionDto>();
        var sepa = listRows.Single(r =>
            r.BankAccountId == bankAccount.Id && r.Description == "SpotifyNL P0102A103D"
        );
        var fx = listRows.Single(r =>
            r.BankAccountId == bankAccount.Id && r.Description == "ATM Belarus"
        );

        // List payload does NOT include metadata. Verify by parsing the raw JSON.
        var listJson = await listResponse.Content.ReadAsStringAsync();
        await Assert.That(listJson).DoesNotContain("\"metadata\"");

        using var sepaResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions/{sepa.Id}", UriKind.Relative)
        );
        await Assert.That(sepaResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var sepaDetail = await sepaResponse.Content.ReadFromJsonAsync<BankTransactionDetailDto>();
        await Assert.That(sepaDetail).IsNotNull();
        var sepaMeta = sepaDetail!.Metadata.ToDictionary(
            m => m.Key,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );
        await Assert.That(sepaMeta["Transaction Code"].StringValue).IsEqualTo("IC");
        await Assert.That(sepaMeta["Transaction Type"].StringValue).IsEqualTo("SEPA Direct Debit");
        await Assert.That(sepaMeta.ContainsKey("Foreign Currency Mark Up Amount")).IsFalse();

        using var fxResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions/{fx.Id}", UriKind.Relative)
        );
        await Assert.That(fxResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fxDetail = await fxResponse.Content.ReadFromJsonAsync<BankTransactionDetailDto>();
        await Assert.That(fxDetail).IsNotNull();
        var fxMeta = fxDetail!.Metadata.ToDictionary(
            m => m.Key,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );
        await Assert.That(fxMeta["Transaction Code"].StringValue).IsEqualTo("GM");
        await Assert.That(fxMeta["Term"].StringValue).IsEqualTo("ATM12713");
        await Assert.That(fxMeta["Card Sequence Number"].StringValue).IsEqualTo("008");
        await Assert.That(fxMeta["Foreign Currency Mark Up Amount"].IntegerValue).IsEqualTo(45L);
        await Assert.That(fxMeta["Foreign Currency Mark Up Code"].StringValue).IsEqualTo("EUR");
        await Assert.That(fxMeta["Foreign Currency Fee Amount"].IntegerValue).IsEqualTo(225L);
        await Assert.That(fxMeta["Foreign Currency Fee Code"].StringValue).IsEqualTo("EUR");
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
            CounterpartyId: null,
            Type: "Current",
            ImporterKey: "Ing.CurrentAccount.V1"
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

internal sealed record BankTransactionDetailDto(
    Guid Id,
    Guid BankAccountId,
    DateOnly BookingDate,
    string Description,
    IReadOnlyList<BankTransactionMetadataEntryDto> Metadata
);

internal sealed record BankTransactionMetadataEntryDto(
    string Key,
    string? StringValue,
    long? IntegerValue
);
