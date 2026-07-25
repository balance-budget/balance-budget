using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Importers;
using Balance.Integration.Ing.Parsers;
using Balance.Services.Contracts;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngCreditCardCsvExtractorTests
{
    private const string DutchExport = "CreditCard_123456789000_01-01-2026_31-01-2026.csv";
    private const string EnglishExport = "CreditCard_123456789000_01-03-2026_31-03-2026.csv";
    private const string CardNumber = "1234 **** **** 5678";
    private const string FundingIban = "NL69INGB0123456789";

    [Test]
    public async Task Maps_the_Dutch_export(CancellationToken cancellationToken)
    {
        var rows = await ExtractAsync(DutchExport, cancellationToken);

        // The CSV is most-recent-first; the extractor reverses so the oldest row lands first.
        await Assert.That(rows.Count).IsEqualTo(10);
        await Assert.That(rows[0].BookingDate).IsEqualTo(new DateOnly(2026, 1, 3));
        await Assert.That(rows[^1].BookingDate).IsEqualTo(new DateOnly(2026, 1, 20));

        var purchase = rows[^1];
        await Assert.That(purchase.Description).IsEqualTo("XDT*BEACH RESORT IDN");
        await Assert.That(purchase.CounterpartyName).IsEqualTo("XDT*BEACH RESORT IDN");
        await Assert.That(purchase.Money.Amount).IsEqualTo(-86981L);
        await Assert.That(purchase.ForeignAmount).IsEqualTo(1_742_340_000L);
        await Assert.That(purchase.ForeignCurrencyCode).IsEqualTo("IDR");
        await Assert.That(purchase.ExchangeRate).IsEqualTo(0.0000489m);
        // A merchant row's counterparty is the merchant, so it gets no counterparty account.
        await Assert.That(purchase.CounterpartyAccountNumber).IsNull();

        // Rows that move money between the card and its funding account name no card, so they take
        // the counterparty from the configured Funding account — which is what lets the card-side
        // pay-down attach to the current-account leg.
        var payDown = rows.Single(row => row.Description == "Transfer to credit card");
        await Assert.That(payDown.Money.Amount).IsEqualTo(148318L);
        await Assert.That(payDown.CounterpartyAccountNumber).IsEqualTo(FundingIban);

        var withdrawal = rows.Single(row =>
            row.Description == "Withdrawal positive balance credit card"
        );
        await Assert.That(withdrawal.Money.Amount).IsEqualTo(-44364L);
        await Assert.That(withdrawal.CounterpartyAccountNumber).IsEqualTo(FundingIban);

        // Identical rows must both survive dedup, so their hashes differ.
        var twins = rows.Where(row => row.Description == "DL *Taxi Brasil BRA").ToList();
        await Assert.That(twins.Count).IsEqualTo(2);
        await Assert.That(twins[0].RowHash).IsNotEqualTo(twins[1].RowHash);
    }

    [Test]
    public async Task Both_dialects_map_to_the_same_transactions(
        CancellationToken cancellationToken
    )
    {
        var dutch = await ExtractAsync(DutchExport, cancellationToken);
        var english = await ExtractAsync(EnglishExport, cancellationToken);

        // The two fixtures are the same ten transactions two months apart, so every mapped field
        // except the raw row text has to agree. This is what pins the English vocabulary down:
        // "Debit" is Betaling, "Miscellaneous" is Diversen, and "Payment" is Incasso or Ontvangst
        // depending on whether the row names a card.
        await Assert.That(english.Count).IsEqualTo(dutch.Count);

        for (var i = 0; i < dutch.Count; i++)
        {
            var expected = dutch[i];
            var actual = english[i];

            await Assert.That(actual.BookingDate).IsEqualTo(expected.BookingDate.AddMonths(2));
            await Assert.That(actual.Description).IsEqualTo(expected.Description);
            await Assert.That(actual.Money.Amount).IsEqualTo(expected.Money.Amount);
            await Assert.That(actual.CounterpartyName).IsEqualTo(expected.CounterpartyName);
            await Assert
                .That(actual.CounterpartyAccountNumber)
                .IsEqualTo(expected.CounterpartyAccountNumber);
            await Assert.That(actual.ForeignAmount).IsEqualTo(expected.ForeignAmount);
            await Assert.That(actual.ForeignCurrencyCode).IsEqualTo(expected.ForeignCurrencyCode);
            await Assert.That(actual.ExchangeRate).IsEqualTo(expected.ExchangeRate);
            await Assert
                .That(Metadata(actual, "Transaction Type"))
                .IsEqualTo(Metadata(expected, "Transaction Type"));
            await Assert
                .That(Metadata(actual, "Foreign Currency Mark Up Amount"))
                .IsEqualTo(Metadata(expected, "Foreign Currency Mark Up Amount"));
        }
    }

    [Test]
    public async Task Anchors_detection_on_the_first_row_that_names_a_card(
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(DutchExport);
        var identity = await BuildExtractor()
            .TryIdentifyAsync(new ImportFile(DutchExport, stream), cancellationToken);

        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.ImporterKey).IsEqualTo("Ing.CreditCard");
        await Assert.That(identity.SupportedType).IsEqualTo(BankAccountType.Card);
        await Assert.That(identity.AccountAnchor).IsEqualTo(Normalize(CardNumber));
    }

    [Test]
    public async Task Rejects_a_statement_for_another_card(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(DutchExport);
        var result = await BuildExtractor()
            .ExtractAsync(OwnedCard("9999 **** **** 1111"), stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);
    }

    private static async Task<IReadOnlyList<BankTransaction>> ExtractAsync(
        string fileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(fileName);
        var result = await BuildExtractor().ExtractAsync(OwnedCard(), stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }

    private static IngCreditCardTransactionExtractor BuildExtractor() =>
        new(
            new IIngCreditCardStatementParser[]
            {
                new IngLegacyCreditCardStatementParser(),
                new IngModernCreditCardStatementParser(),
                new IngCreditCardCsvStatementParser(),
            }
        );

    private static BankAccount OwnedCard(string cardIdentifier = CardNumber) =>
        new()
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Type = BankAccountType.Card,
            CardIdentifier = cardIdentifier,
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = new AccountId(Guid.CreateVersion7()),
            FundingBankAccount = new BankAccount
            {
                Id = new BankAccountId(Guid.CreateVersion7()),
                Type = BankAccountType.Current,
                Iban = FundingIban,
                CurrencyCode = new CurrencyCode("EUR"),
                AccountId = new AccountId(Guid.CreateVersion7()),
            },
        };

    private static string? Metadata(BankTransaction row, string key) =>
        row
            .Metadata.Where(m => m.Key!.Name == key)
            .Select(m => m.StringValue ?? m.IntegerValue?.ToString(null as IFormatProvider))
            .FirstOrDefault();

    private static string Normalize(string value) =>
        value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static FileStream OpenFixture(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Integrations", "Ing", fileName));
}
