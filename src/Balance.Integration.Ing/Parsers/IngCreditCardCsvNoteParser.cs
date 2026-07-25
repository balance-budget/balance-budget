using System.Globalization;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Parsers;

// The 'Mededelingen' / 'Notifications' column of a credit-card CSV row. Same grammar as the
// current-account note (see IngNotePrefixScanner) with a different vocabulary, comma-delimited
// pairs, and a number culture that follows the export language (ADR 0038).
internal static class IngCreditCardCsvNoteParser
{
    private static readonly IngNotePrefixScanner<CreditCardCsvNote> DutchScanner = Build(
        CreditCardCsvDialect.Dutch
    );

    private static readonly IngNotePrefixScanner<CreditCardCsvNote> EnglishScanner = Build(
        CreditCardCsvDialect.English
    );

    public static CreditCardCsvNote ParseNote(string note, CreditCardCsvDialect dialect)
    {
        var result = new CreditCardCsvNote();
        var scanner = dialect is CreditCardCsvDialect.Dutch ? DutchScanner : EnglishScanner;
        result.Other = scanner.Scan(note, result);
        return result;
    }

    private static IngNotePrefixScanner<CreditCardCsvNote> Build(CreditCardCsvDialect dialect)
    {
        var culture = dialect.Culture();
        var dateFormat = dialect.NoteDateFormat();

        // The mark-up prefix carries its currency in the label ("Koersopslag (EUR)"), and ING only
        // ever charges it in the card's own currency. A label naming another currency simply would
        // not match, leaving the value in the note's leftover text rather than misreading it.
        var (transactionDate, cardNumber, amount, rate, markUp) =
            dialect is CreditCardCsvDialect.Dutch
                ? ("Transactiedatum", "Kaartnummer", "Bedrag", "Koers", "Koersopslag (EUR)")
                : ("Transaction date", "Card number", "Amount", "Exchange rate", "Fee (EUR)");

        var setters = new Dictionary<string, Action<CreditCardCsvNote, string>>(
            StringComparer.Ordinal
        )
        {
            [transactionDate] = (note, value) =>
            {
                if (
                    DateOnly.TryParseExact(
                        value,
                        dateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsed
                    )
                )
                    note.TransactionDate = parsed;
            },
            [cardNumber] = (note, value) => note.CardNumber = value,
            [amount] = (note, value) =>
                note.ForeignCurrencyAmount = CurrencyAmount.TryParse(value, culture),
            [rate] = (note, value) =>
            {
                if (decimal.TryParse(value, NumberStyles.Number, culture, out var parsed))
                    note.ForeignCurrencyRate = parsed;
            },
            [markUp] = (note, value) =>
            {
                if (decimal.TryParse(value, NumberStyles.Number, culture, out var parsed))
                    note.ForeignCurrencyMarkUp = new CurrencyAmount(parsed, "EUR");
            },
        };

        // Pairs are comma-delimited, and the lookahead to the next prefix leaves that comma on the
        // end of the preceding value.
        return new IngNotePrefixScanner<CreditCardCsvNote>(setters, ',');
    }
}
