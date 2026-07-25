using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngCreditCardCsvStatementParserTests
{
    private const string DutchExport = "CreditCard_123456789000_01-01-2026_31-01-2026.csv";
    private const string EnglishExport = "CreditCard_123456789000_01-03-2026_31-03-2026.csv";

    [Test]
    [Arguments(DutchExport)]
    [Arguments(EnglishExport)]
    public async Task Recognizes_both_dialects(string fileName, CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(fileName);
        var source = new IngCreditCardStatementSource(stream);

        await Assert
            .That(
                await new IngCreditCardCsvStatementParser().CanParseAsync(source, cancellationToken)
            )
            .IsTrue();
    }

    [Test]
    public async Task Parses_the_Dutch_export(CancellationToken cancellationToken)
    {
        var statement = await ParseAsync(DutchExport, cancellationToken);

        // The CSV never names the funding account.
        await Assert.That(statement.LinkedAccount).IsNull();
        await Assert.That(statement.Rows.Count).IsEqualTo(10);

        var foreign = statement.Rows[0];
        await Assert.That(foreign.Date).IsEqualTo(new DateOnly(2026, 1, 20));
        await Assert.That(foreign.Description).IsEqualTo("XDT*BEACH RESORT IDN");
        await Assert.That(foreign.TransactionType).IsEqualTo(CreditCardTransactionType.Payment);
        // "Af" — a purchase is negative on the card.
        await Assert.That(foreign.Amount).IsEqualTo(-869.81m);
        await Assert.That(foreign.CardNumber).IsEqualTo("1234 **** **** 5678");
        await Assert.That(foreign.TransactionDate).IsEqualTo(new DateOnly(2026, 1, 20));

        // "17.423.400,00 IDR" under nl-NL. Read under the wrong culture this parses to null.
        await Assert.That(foreign.ForeignCurrencyAmount!.Amount).IsEqualTo(17_423_400.00m);
        await Assert.That(foreign.ForeignCurrencyAmount!.CurrencyCode).IsEqualTo("IDR");
        // "Koers: 0,0000489" — under nl-NL the dot would be a group separator, giving 489.
        await Assert.That(foreign.ForeignCurrencyRate).IsEqualTo(0.0000489m);
        // Dutch calls the mark-up "Koersopslag"; English calls the same field "Fee".
        await Assert.That(foreign.ForeignCurrencyMarkUp!.Amount).IsEqualTo(17.81m);
        await Assert.That(foreign.ForeignCurrencyMarkUp!.CurrencyCode).IsEqualTo("EUR");

        // The converted amount plus the mark-up is what the card was charged.
        await Assert
            .That(
                decimal.Round(
                    foreign.ForeignCurrencyAmount!.Amount * foreign.ForeignCurrencyRate!.Value
                        + foreign.ForeignCurrencyMarkUp!.Amount,
                    2
                )
            )
            .IsEqualTo(-foreign.Amount);

        var payDown = statement.Rows[1];
        await Assert.That(payDown.Description).IsEqualTo("Transfer to credit card");
        await Assert
            .That(payDown.TransactionType)
            .IsEqualTo(CreditCardTransactionType.Miscellaneous);
        await Assert.That(payDown.Amount).IsEqualTo(1483.18m);
        // No card was involved, which is what marks this as a funding-account transfer.
        await Assert.That(payDown.CardNumber).IsEmpty();
        await Assert.That(payDown.ForeignCurrencyAmount).IsNull();

        var refund = statement.Rows[6];
        await Assert.That(refund.Description).IsEqualTo("Event Tickets Berlin DEU");
        await Assert.That(refund.TransactionType).IsEqualTo(CreditCardTransactionType.Receipt);
        await Assert.That(refund.Amount).IsEqualTo(443.64m);
    }

    [Test]
    public async Task Parses_the_English_export(CancellationToken cancellationToken)
    {
        var statement = await ParseAsync(EnglishExport, cancellationToken);

        await Assert.That(statement.LinkedAccount).IsNull();
        await Assert.That(statement.Rows.Count).IsEqualTo(10);

        var foreign = statement.Rows[0];
        await Assert.That(foreign.Date).IsEqualTo(new DateOnly(2026, 3, 20));
        // "869.81" under en-US. Read under nl-NL this would be 86981.
        await Assert.That(foreign.Amount).IsEqualTo(-869.81m);
        // The note's date is dd/MM/yyyy here, dd-MM-yyyy in the Dutch export.
        await Assert.That(foreign.TransactionDate).IsEqualTo(new DateOnly(2026, 3, 20));
        await Assert.That(foreign.ForeignCurrencyAmount!.Amount).IsEqualTo(17_423_400.00m);
        await Assert.That(foreign.ForeignCurrencyRate).IsEqualTo(0.0000489m);
        await Assert.That(foreign.ForeignCurrencyMarkUp!.Amount).IsEqualTo(17.81m);

        // "1,483.18" — under nl-NL this would be 1.48.
        await Assert.That(statement.Rows[1].Amount).IsEqualTo(1483.18m);
    }

    [Test]
    public async Task English_Payment_resolves_by_card_number(CancellationToken cancellationToken)
    {
        var statement = await ParseAsync(EnglishExport, cancellationToken);

        // English labels both rows "Payment"; Dutch distinguishes Incasso from Ontvangst. The
        // pay-down names no card, the refund does.
        var payDown = statement.Rows[3];
        await Assert.That(payDown.Description).IsEqualTo("AFLOSSING");
        await Assert.That(payDown.CardNumber).IsEmpty();
        await Assert.That(payDown.TransactionType).IsEqualTo(CreditCardTransactionType.DirectDebit);

        var refund = statement.Rows[6];
        await Assert.That(refund.Description).IsEqualTo("Event Tickets Berlin DEU");
        await Assert.That(refund.CardNumber).IsNotEmpty();
        await Assert.That(refund.TransactionType).IsEqualTo(CreditCardTransactionType.Receipt);
    }

    [Test]
    [Arguments(DutchExport)]
    [Arguments(EnglishExport)]
    public async Task Identical_rows_hash_distinctly(
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var statement = await ParseAsync(fileName, cancellationToken);

        // The last two rows are byte-identical (same day, merchant and amount, and the CSV carries
        // no running balance to tell them apart). Only the second is marked, so an ordinary unique
        // row still stores exactly the bank's line.
        var first = statement.Rows[8];
        var second = statement.Rows[9];

        await Assert.That(first.RawRecord).StartsWith("\"2026-");
        await Assert.That(second.RawRecord).IsEqualTo($"2|{first.RawRecord}");
    }

    private static async Task<CreditCardStatement> ParseAsync(
        string fileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(fileName);
        var source = new IngCreditCardStatementSource(stream);
        return await new IngCreditCardCsvStatementParser().ParseStatementAsync(
            source,
            cancellationToken
        );
    }

    private static FileStream OpenFixture(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Integrations", "Ing", fileName));
}
