using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngNoteParser : IIngNoteParser
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("nl-NL");

    // Each entry either assigns directly (for fields whose value is just the raw string), or routes
    // through a TryParse-based setter that leaves the field at its default when the value is
    // malformed. ING exports are well-formed at the CSV-column level, but individual note-prefix
    // values occasionally drift (truncated timestamps, missing currency codes, locale switches
    // mid-export) — degrade silently rather than sink the whole import.
    private static readonly Dictionary<string, Action<IngNote, string>> Prefixes = Build([
        (["Naam", "Name"], (n, v) => n.Name = v),
        (["Omschrijving", "Description"], (n, v) => n.Description = v),
        (["IBAN"], (n, v) => n.Iban = v),
        (["Pasvolgnr", "Card sequence no."], (n, v) => n.CardSequence = CardSequence.TryParse(v)),
        (["Transactie", "Transaction"], (n, v) => n.Transaction = v),
        (["Term"], (n, v) => n.Term = v),
        (
            ["Valuta", "Currency"],
            (n, v) => n.ForeignCurrencyAmount = CurrencyAmount.TryParse(v, Culture)
        ),
        (["Koers", "Rate"], SetForeignCurrencyRate),
        (
            ["Opslag", "Mark-up"],
            (n, v) => n.ForeignCurrencyMarkUp = CurrencyAmount.TryParse(v, Culture)
        ),
        (["Kosten", "Fee"], (n, v) => n.ForeignCurrencyFee = CurrencyAmount.TryParse(v, Culture)),
        (["Valutadatum", "Value date"], SetValueDate),
        (["Datum/Tijd", "Date/time"], SetDateTime),
        (["Kenmerk", "Reference"], (n, v) => n.Reference = v),
        (["Machtiging ID", "Mandate ID"], (n, v) => n.MandateId = v),
        (["Incassant ID", "Creditor ID"], (n, v) => n.Creditor = SepaDirectDebitCreditor.Parse(v)),
        (["Overige partij", "Other party"], (n, v) => n.OtherParty = v),
    ]);

    private static readonly IngNotePrefixScanner<IngNote> Scanner = new(Prefixes);

    public IngNote ParseNote(string note)
    {
        var result = new IngNote { Original = note };
        result.Other = Scanner.Scan(note, result);
        return result;
    }

    private static Dictionary<string, Action<IngNote, string>> Build(
        IEnumerable<(string[] Variants, Action<IngNote, string> Setter)> entries
    ) =>
        entries
            .SelectMany(entry => entry.Variants.Select(variant => (variant, entry.Setter)))
            .ToDictionary(x => x.variant, x => x.Setter, StringComparer.Ordinal);

    private static void SetForeignCurrencyRate(IngNote note, string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, Culture, out var rate))
            note.ForeignCurrencyRate = rate;
    }

    private static void SetValueDate(IngNote note, string value)
    {
        if (DateOnly.TryParse(value, Culture, out var date))
            note.ValueDate = date;
    }

    private static void SetDateTime(IngNote note, string value)
    {
        if (DateTime.TryParse(value, Culture, out var dateTime))
            note.DateTime = dateTime;
    }
}
