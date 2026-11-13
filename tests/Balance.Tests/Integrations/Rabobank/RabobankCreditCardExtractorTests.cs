using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Rabobank.Importers;
using Balance.Integration.Rabobank.Parsers;
using Balance.Services.Contracts;

namespace Balance.Tests.Integrations.Rabobank;

internal sealed class RabobankCreditCardExtractorTests
{
    private const string Export = "RA_CC_1234_20260101_20260131.csv";
    private const string CardIdentifier = "************1234";
    private const string FundingIban = "NL12RABO0123456789";

    [Test]
    public async Task Splits_merchant_rows_into_fixed_columns(CancellationToken cancellationToken)
    {
        var rows = await ExtractAsync(OwnedCard(), cancellationToken);

        await Assert.That(rows.Count).IsEqualTo(8);
        await Assert.That(rows.All(row => row.ImporterKey == "Rabobank.CreditCard")).IsTrue();

        var purchase = rows[1];
        await Assert.That(purchase.Description).IsEqualTo("Example Shop NL*AB12CD AMSTERDAM NLD");
        await Assert.That(purchase.CounterpartyName).IsEqualTo("Example Shop NL*AB12CD");
        await Assert.That(purchase.CounterpartyAccountNumber).IsNull();
        await Assert.That(purchase.Money.Amount).IsEqualTo(-1359L);
        await Assert.That(Metadata(purchase, "Card Location")).IsEqualTo("AMSTERDAM");
        await Assert.That(Metadata(purchase, "Card Country")).IsEqualTo("NLD");
        await Assert.That(Metadata(purchase, "Wallet")).IsNull();
        await Assert.That(Metadata(purchase, "Funding Account")).IsEqualTo(FundingIban);

        var refund = rows[3];
        await Assert.That(refund.Money.Amount).IsEqualTo(1574L);
        await Assert.That(refund.CounterpartyName).IsEqualTo("SAMPLE STORE.COM");
        await Assert.That(refund.CounterpartyAccountNumber).IsNull();
        await Assert.That(Metadata(refund, "Wallet")).IsEqualTo("Google Pay");

        // The city is cut off at its column width and runs into the country code.
        var truncatedCity = rows[4];
        await Assert.That(truncatedCity.CounterpartyName).IsEqualTo("SAMPLE GARAGE");
        await Assert.That(truncatedCity.Money.Amount).IsEqualTo(-116316L);
        await Assert.That(Metadata(truncatedCity, "Card Location")).IsEqualTo("ALPHEN AAN DE");
        await Assert.That(Metadata(truncatedCity, "Card Country")).IsEqualTo("NLD");
    }

    [Test]
    public async Task Gives_funding_transfers_the_funding_account(
        CancellationToken cancellationToken
    )
    {
        var rows = await ExtractAsync(OwnedCard(), cancellationToken);

        var transfers = rows.Where(row => row.CounterpartyAccountNumber is not null).ToList();
        await Assert.That(transfers.Count).IsEqualTo(3);
        await Assert
            .That(transfers.All(row => row.CounterpartyAccountNumber == FundingIban))
            .IsTrue();
        await Assert.That(transfers.All(row => row.CounterpartyName is null)).IsTrue();
        await Assert.That(transfers[0].Description).IsEqualTo("Verrekening vorig overzicht");
        await Assert.That(transfers[1].Money.Amount).IsEqualTo(5000L);
        await Assert.That(transfers[2].Money.Amount).IsEqualTo(-2000L);
    }

    [Test]
    public async Task Uses_the_stated_funding_account_when_none_is_configured(
        CancellationToken cancellationToken
    )
    {
        var card = OwnedCard();
        card.FundingBankAccount = null;

        var rows = await ExtractAsync(card, cancellationToken);

        await Assert.That(rows[0].CounterpartyAccountNumber).IsEqualTo(FundingIban);
    }

    [Test]
    public async Task Rejects_a_statement_naming_another_funding_account(
        CancellationToken cancellationToken
    )
    {
        var card = OwnedCard();
        card.FundingBankAccount!.Iban = "NL98RABO0000000001";

        await using var stream = OpenFixture(Export);
        var result = await BuildExtractor().ExtractAsync(card, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);
    }

    [Test]
    public async Task Rejects_a_statement_for_another_card(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(Export);
        var result = await BuildExtractor()
            .ExtractAsync(OwnedCard("************9999"), stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);
    }

    [Test]
    public async Task Identifies_the_card_from_the_filename_and_the_content(
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(Export);
        var fromName = await BuildExtractor()
            .TryIdentifyAsync(new ImportFile(Export, stream), cancellationToken);
        var fromContent = await BuildExtractor()
            .TryIdentifyAsync(new ImportFile("export.csv", stream), cancellationToken);

        await Assert.That(fromName!.AccountAnchor).IsEqualTo(CardIdentifier);
        await Assert.That(fromName.SupportedType).IsEqualTo(BankAccountType.Card);
        await Assert.That(fromContent!.AccountAnchor).IsEqualTo(CardIdentifier);
    }

    [Test]
    public async Task Does_not_recognize_an_account_export(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture("CSV_A_NL12RABO0123456789_EUR_20260101_20260131.csv");
        var identity = await BuildExtractor()
            .TryIdentifyAsync(new ImportFile("export.csv", stream), cancellationToken);

        await Assert.That(identity).IsNull();
    }

    private static async Task<IReadOnlyList<BankTransaction>> ExtractAsync(
        BankAccount card,
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(Export);
        var result = await BuildExtractor().ExtractAsync(card, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }

    private static RabobankCreditCardTransactionExtractor BuildExtractor() =>
        new(new RabobankCreditCardStatementParser());

    private static BankAccount OwnedCard(string cardIdentifier = CardIdentifier) =>
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

    private static FileStream OpenFixture(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Integrations", "Rabobank", fileName));
}
