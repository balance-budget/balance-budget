using System.Text;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Importers;
using Balance.Integration.Ing.Parsers;
using Balance.Services.Contracts;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngBankTransactionExtractorTests
{
    private const string Header =
        "\"Date\";\"Name / Description\";\"Account\";\"Counterparty\";\"Code\";"
        + "\"Debit/credit\";\"Amount (EUR)\";\"Transaction type\";\"Notifications\";"
        + "\"Resulting balance\";\"Tag\"";

    private static IngBankTransactionExtractor BuildExtractor() =>
        new(new IngCurrentAccountStatementParser(), new IngNoteParser());

    private static BankAccount OwnedEurAccount(string iban = "NL69INGB0123456789") =>
        new()
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = iban,
            AccountNumber = null,
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = new AccountId(Guid.CreateVersion7()),
            CounterpartyId = null,
        };

    private static MemoryStream CsvStream(params string[] rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var row in rows)
            sb.AppendLine(row);
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Test]
    public async Task Extracts_rows_with_signed_money_description_and_counterparty(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"72,30\";\"Payment terminal\";\"Card sequence no.: 001 31-01-2026 09:26\";\"1183,44\";\"\"",
            "\"20260130\";\"Coolblue BV\";\"NL69INGB0123456789\";\"NL22INGB3141592653\";\"GT\";"
                + "\"Credit\";\"59,95\";\"Online Banking\";\"Name: Coolblue BV\";\"1412,95\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var rows = result.Value!;
        await Assert.That(rows).Count().IsEqualTo(2);

        // ING is most-recent-first; the extractor reverses so the oldest row lands first.
        await Assert.That(rows[0].BookingDate).IsEqualTo(new DateOnly(2026, 1, 30));
        await Assert.That(rows[0].Money.Amount).IsEqualTo(5995L);
        await Assert.That(rows[0].Money.CurrencyCode).IsEqualTo(new CurrencyCode("EUR"));
        await Assert.That(rows[0].Description).IsEqualTo("Coolblue BV");
        await Assert.That(rows[0].CounterpartyName).IsEqualTo("Coolblue BV");
        await Assert.That(rows[0].CounterpartyAccountNumber).IsEqualTo("NL22INGB3141592653");

        await Assert.That(rows[1].BookingDate).IsEqualTo(new DateOnly(2026, 1, 31));
        await Assert.That(rows[1].Money.Amount).IsEqualTo(-7230L);
        await Assert.That(rows[1].Description).IsEqualTo("Etos");
        await Assert.That(rows[1].CounterpartyName).IsEqualTo("Etos");
        await Assert.That(rows[1].CounterpartyAccountNumber).IsNull();
    }

    [Test]
    public async Task Description_prefers_structured_description_from_notifications_over_name(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260129\";\"Booking.com\";\"NL69INGB0123456789\";\"NL22ABNA2000003006\";\"ID\";"
                + "\"Debit\";\"32,15\";\"iDEAL\";"
                + "\"Naam: Booking.com Omschrijving: Online bestelling 1278631 IBAN: "
                + "NL22ABNA2000003006 Kenmerk: 1278631\";\"1277,49\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.Description).IsEqualTo("Online bestelling 1278631");
        await Assert.That(row.CounterpartyName).IsEqualTo("Booking.com");
        await Assert.That(row.CounterpartyAccountNumber).IsEqualTo("NL22ABNA2000003006");
    }

    [Test]
    public async Task Rows_are_reversed_so_oldest_comes_first(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"A\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"1,00\";\"Payment terminal\";\"x\";\"100,00\";\"\"",
            "\"20260130\";\"B\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"2,00\";\"Payment terminal\";\"y\";\"99,00\";\"\"",
            "\"20260129\";\"C\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"3,00\";\"Payment terminal\";\"z\";\"97,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var rows = result.Value!;
        await Assert.That(rows[0].BookingDate).IsEqualTo(new DateOnly(2026, 1, 29));
        await Assert.That(rows[1].BookingDate).IsEqualTo(new DateOnly(2026, 1, 30));
        await Assert.That(rows[2].BookingDate).IsEqualTo(new DateOnly(2026, 1, 31));
    }

    [Test]
    public async Task Savings_number_fallback_from_name_description(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Naar Oranje Spaarrekening D12345678\";\"NL69INGB0123456789\";\"\";"
                + "\"OV\";\"Debit\";\"100,00\";\"Internal transfer\";\"\";\"500,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.CounterpartyAccountNumber).IsEqualTo("D12345678");
    }

    [Test]
    public async Task Savings_number_fallback_from_other_notifications_when_description_lacks_it(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // 'Internal transfer' carries no D######## itself; 'Mededelingen' has it as free-text
        // leftover (no known prefix), so it lands in IngNote.Other.
        await using var stream = CsvStream(
            "\"20260131\";\"Internal transfer\";\"NL69INGB0123456789\";\"\";\"OV\";\"Debit\";"
                + "\"100,00\";\"Internal transfer\";\"Transfer to D87654321 confirmed\";"
                + "\"500,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.CounterpartyAccountNumber).IsEqualTo("D87654321");
    }

    [Test]
    public async Task Rejects_row_with_no_usable_description(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        // Empty 'Naam / Omschrijving' AND a 'Mededelingen' that carries no 'Omschrijving:'.
        await using var stream = CsvStream(
            "\"20260131\";\"\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"Kenmerk: 12345\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportFormatInvalid);
    }

    [Test]
    public async Task Populates_RawSource_and_RowHash_per_row(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"72,30\";\"Payment terminal\";\"Card sequence no.: 001 31-01-2026 09:26\";\"1183,44\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.RawSource).Contains("Etos");
        await Assert.That(row.RawSource).Contains("NL69INGB0123456789");
        await Assert.That(row.RowHash).Matches("^[0-9a-f]{64}$");
    }

    [Test]
    public async Task Accepts_iban_with_spaces_on_bank_account(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount(iban: "NL69 INGB 0123 4567 89");
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Matches_against_AccountNumber_when_Iban_not_set(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = null,
            AccountNumber = "0123456789",
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = new AccountId(Guid.CreateVersion7()),
        };
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Rejects_when_first_row_account_does_not_match(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount(iban: "NL69INGB0123456789");
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL11RABO9999999999\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);
    }

    [Test]
    public async Task Rejects_when_rows_have_diverging_account_values(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount(iban: "NL69INGB0123456789");
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\"",
            "\"20260131\";\"Other\";\"NL11RABO9999999999\";\"\";\"BA\";\"Debit\";"
                + "\"5,00\";\"Payment terminal\";\"y\";\"95,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportAccountColumnDivergence);
    }

    [Test]
    public async Task Rejects_when_bank_account_is_not_owned(CancellationToken cancellationToken)
    {
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = "NL69INGB0123456789",
            CurrencyCode = null,
            AccountId = null,
            CounterpartyId = new CounterpartyId(Guid.CreateVersion7()),
        };
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportBankAccountNotOwned);
    }

    [Test]
    public async Task Rejects_when_currency_is_not_EUR(CancellationToken cancellationToken)
    {
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = "NL69INGB0123456789",
            CurrencyCode = new CurrencyCode("USD"),
            AccountId = new AccountId(Guid.CreateVersion7()),
        };
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportCurrencyMismatch);
    }

    [Test]
    public async Task Rejects_malformed_csv(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"not-a-number\";\"Payment terminal\";\"x\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportFormatInvalid);
    }

    [Test]
    public async Task Does_not_deduplicate_identical_rows(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        var row =
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
            + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\"";
        await using var stream = CsvStream(row, row);

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Count).IsEqualTo(2);
        await Assert.That(result.Value![0].RowHash).IsEqualTo(result.Value![1].RowHash);
    }

    [Test]
    public async Task Empty_csv_returns_empty_list(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream();

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Stamps_ImporterKey_on_every_row(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"\"",
            "\"20260130\";\"Coolblue BV\";\"NL69INGB0123456789\";\"NL22INGB3141592653\";\"GT\";"
                + "\"Credit\";\"59,95\";\"Online Banking\";\"Name: Coolblue BV\";\"1412,95\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var rows = result.Value!;
        await Assert.That(rows[0].ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
        await Assert.That(rows[1].ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
    }

    [Test]
    public async Task Populates_SEPA_columns_for_direct_debit_row(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // SEPA direct-debit row: Naam/Omschrijving/IBAN/Kenmerk/Machtiging ID/Incassant ID/Valutadatum.
        await using var stream = CsvStream(
            "\"20260131\";\"SPOTIFY BY ADYEN\";\"NL69INGB0123456789\";\"NL48ABNA0502830042\";"
                + "\"ID\";\"Debit\";\"9,99\";\"SEPA Direct Debit\";"
                + "\"Naam: SPOTIFY BY ADYEN Omschrijving: SpotifyNL P0102A103D "
                + "IBAN: NL48ABNA0502830042 Kenmerk: D1815340503797720C "
                + "Machtiging ID: 4815225945787361 Incassant ID: NL74ZZZ546978200017 "
                + "Doorlopende incasso Overige partij: SpotifyNL Valutadatum: 31-01-2026\";"
                + "\"500,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.ValueDate).IsEqualTo(new DateOnly(2026, 1, 31));
        await Assert.That(row.Reference).IsEqualTo("D1815340503797720C");
        await Assert.That(row.MandateId).IsEqualTo("4815225945787361");
        await Assert.That(row.SepaCreditorId).IsEqualTo("NL74ZZZ546978200017");
        await Assert.That(row.ForeignAmount).IsNull();
        await Assert.That(row.ForeignCurrencyCode).IsNull();
        await Assert.That(row.ExchangeRate).IsNull();
        await Assert.That(row.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
    }

    [Test]
    public async Task Populates_FX_columns_for_foreign_currency_row(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // FX withdrawal: Valuta + Koers in the note.
        await using var stream = CsvStream(
            "\"20261231\";\"ATM Belarus\";\"NL69INGB0123456789\";\"\";\"GM\";\"Debit\";"
                + "\"40,97\";\"Cash machine\";"
                + "\"Pasvolgnr: 008 30-12-2026 14:20 Transactie: Q71243 Term: ATM12713 "
                + "Valuta: 100,00 BYN Koers: 2,4406776 Opslag: 0,45 EUR "
                + "Kosten: 2,25 EUR Valutadatum: 31-12-2026\";\"459,03\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.ForeignAmount).IsEqualTo(10000L);
        await Assert.That(row.ForeignCurrencyCode).IsEqualTo("BYN");
        await Assert.That(row.ExchangeRate).IsEqualTo(2.4406776m);
        await Assert.That(row.ValueDate).IsEqualTo(new DateOnly(2026, 12, 31));
        await Assert.That(row.MandateId).IsNull();
        await Assert.That(row.SepaCreditorId).IsNull();
        await Assert.That(row.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
    }

    [Test]
    public async Task Promoted_columns_are_null_when_note_lacks_them(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"Pasvolgnr: 001 31-01-2026 09:26\";"
                + "\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        await Assert.That(row.ValueDate).IsNull();
        await Assert.That(row.Reference).IsNull();
        await Assert.That(row.MandateId).IsNull();
        await Assert.That(row.SepaCreditorId).IsNull();
        await Assert.That(row.ForeignAmount).IsNull();
        await Assert.That(row.ForeignCurrencyCode).IsNull();
        await Assert.That(row.ExchangeRate).IsNull();
        // ImporterKey is always stamped, even for rows without note fields.
        await Assert.That(row.ImporterKey).IsEqualTo("Ing.CurrentAccount.V1");
    }

    [Test]
    public async Task Populates_metadata_entries_from_statement_columns(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"Payment terminal\";\"x\";\"100,00\";\"Groceries\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        var byName = row.Metadata.ToDictionary(
            m => m.Key!.Name,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );

        await Assert.That(byName["Tags"].StringValue).IsEqualTo("Groceries");
        await Assert.That(byName["Transaction Code"].StringValue).IsEqualTo("BA");
        await Assert.That(byName["Transaction Type"].StringValue).IsEqualTo("Payment terminal");
    }

    [Test]
    public async Task Populates_metadata_entries_for_sepa_direct_debit_row(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // SEPA direct-debit row with a creditor name and OtherParty.
        await using var stream = CsvStream(
            "\"20260131\";\"SPOTIFY BY ADYEN\";\"NL69INGB0123456789\";\"NL48ABNA0502830042\";"
                + "\"IC\";\"Debit\";\"9,99\";\"SEPA Direct Debit\";"
                + "\"Naam: SPOTIFY BY ADYEN Omschrijving: SpotifyNL P0102A103D "
                + "IBAN: NL48ABNA0502830042 Kenmerk: D1815340503797720C "
                + "Machtiging ID: 4815225945787361 Incassant ID: NL74ZZZ546978200017 "
                + "Spotify Sweden AB Overige partij: SpotifyNL Valutadatum: 31-01-2026\";"
                + "\"500,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        var byName = row.Metadata.ToDictionary(
            m => m.Key!.Name,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );

        await Assert.That(byName["SEPA Description"].StringValue).IsEqualTo("Spotify Sweden AB");
        await Assert.That(byName["SEPA Other Party"].StringValue).IsEqualTo("SpotifyNL");
        await Assert.That(byName["Transaction Code"].StringValue).IsEqualTo("IC");
    }

    [Test]
    public async Task Populates_metadata_entries_for_fx_row(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedEurAccount();
        await using var stream = CsvStream(
            "\"20261231\";\"ATM Belarus\";\"NL69INGB0123456789\";\"\";\"GM\";\"Debit\";"
                + "\"40,97\";\"Cash machine\";"
                + "\"Pasvolgnr: 008 30-12-2026 14:20 Transactie: Q71243 Term: ATM12713 "
                + "Valuta: 100,00 BYN Koers: 2,4406776 Opslag: 0,45 EUR "
                + "Kosten: 2,25 EUR Valutadatum: 31-12-2026\";\"459,03\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        var byName = row.Metadata.ToDictionary(
            m => m.Key!.Name,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );

        await Assert.That(byName["Foreign Currency Mark Up Amount"].IntegerValue).IsEqualTo(45L);
        await Assert.That(byName["Foreign Currency Mark Up Code"].StringValue).IsEqualTo("EUR");
        await Assert.That(byName["Foreign Currency Fee Amount"].IntegerValue).IsEqualTo(225L);
        await Assert.That(byName["Foreign Currency Fee Code"].StringValue).IsEqualTo("EUR");
        await Assert.That(byName["Card Sequence Number"].StringValue).IsEqualTo("008");
        await Assert.That(byName["Term"].StringValue).IsEqualTo("ATM12713");
        await Assert.That(byName["Transaction Code"].StringValue).IsEqualTo("GM");
    }

    [Test]
    public async Task Metadata_preserves_unparsed_note_leftover_under_IngNote_Other(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // 'Internal transfer' carries no D######## itself; 'Mededelingen' has it as free-text
        // leftover (no known prefix), so it lands in IngNote.Other and we surface it for audit.
        await using var stream = CsvStream(
            "\"20260131\";\"Internal transfer\";\"NL69INGB0123456789\";\"\";\"OV\";\"Debit\";"
                + "\"100,00\";\"Internal transfer\";\"Transfer to D87654321 confirmed\";"
                + "\"500,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        var byName = row.Metadata.ToDictionary(
            m => m.Key!.Name,
            m => (m.StringValue, m.IntegerValue),
            StringComparer.Ordinal
        );

        await Assert
            .That(byName["Other Notes"].StringValue)
            .IsEqualTo("Transfer to D87654321 confirmed");
    }

    [Test]
    public async Task Metadata_is_empty_when_no_optional_fields_present(
        CancellationToken cancellationToken
    )
    {
        var bankAccount = OwnedEurAccount();
        // Bare row: empty Tag, empty Notifications, empty Counterparty.
        await using var stream = CsvStream(
            "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
                + "\"10,00\";\"\";\"\";\"100,00\";\"\""
        );

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        var row = result.Value![0];
        // Only IngTransactionCode + (empty) Mutatiesoort skipped + (empty) Tag skipped.
        // The Code column is always present so at minimum IngTransactionCode lands.
        var names = row.Metadata.Select(m => m.Key!.Name).ToList();
        await Assert.That(names).Contains("Transaction Code");
        await Assert.That(names).DoesNotContain("Tags");
        await Assert.That(names).DoesNotContain("Transaction Type");
        await Assert.That(names).DoesNotContain("Other Party");
    }
}
