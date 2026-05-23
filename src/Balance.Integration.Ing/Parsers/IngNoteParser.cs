using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngNoteParser : IIngNoteParser
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly Dictionary<Action<IngNote, string>, HashSet<string>> Prefixes = new()
    {
        [(n, v) => n.Name = v] = ["Naam", "Name"],
        [(n, v) => n.Description = v] = ["Omschrijving", "Description"],
        [(n, v) => n.Iban = v] = ["IBAN"],
        [(n, v) => n.CardSequence = CardSequence.Parse(v)] = ["Pasvolgnr", "Card sequence no."],
        [(n, v) => n.Transaction = v] = ["Transactie", "Transaction"],
        [(n, v) => n.Term = v] = ["Term"],
        [(n, v) => n.ForeignCurrencyAmount = CurrencyAmount.Parse(v)] = ["Valuta", "Currency"],
        [(n, v) => n.ForeignCurrencyRate = decimal.Parse(v, Culture)] = ["Koers", "Rate"],
        [(n, v) => n.ForeignCurrencyMarkUp = CurrencyAmount.Parse(v)] = ["Opslag", "Mark-up"],
        [(n, v) => n.ForeignCurrencyFee = CurrencyAmount.Parse(v)] = ["Kosten", "Fee"],
        [(n, v) => n.ValueDate = DateOnly.Parse(v, Culture)] = ["Valutadatum", "Value date"],
        [(n, v) => n.DateTime = DateTime.Parse(v, Culture)] = ["Datum/Tijd", "Date/time"],
        [(n, v) => n.Reference = v] = ["Kenmerk", "Reference"],
        [(n, v) => n.MandateId = v] = ["Machtiging ID", "Mandate ID"],
        [(n, v) => n.Creditor = SepaDirectDebitCreditor.Parse(v)] = ["Incassant ID", "Creditor ID"],
        [(n, v) => n.OtherParty = v] = ["Overige partij", "Other party"],
    };

    private static readonly Dictionary<string, Action<IngNote, string>> CanonicalPrefixes = Prefixes
        .SelectMany(kvp => kvp.Value.Select(variant => (Variant: variant, Action: kvp.Key)))
        .ToDictionary(x => x.Variant, x => x.Action);

    private static readonly string PrefixPattern =
        $"({string.Join("|", CanonicalPrefixes.Keys.Select(Regex.Escape))})";

    private static readonly Regex Pattern = new(
        $"(?<prefix>{PrefixPattern}): (?<value>.*?)(?=(?:{PrefixPattern}):|$)",
        RegexOptions.Compiled
    );

    public IngNote ParseNote(string note)
    {
        var result = new IngNote { Original = note };

        if (string.IsNullOrWhiteSpace(note))
            return result;

        var leftover = Pattern.Replace(
            note,
            match =>
            {
                var prefix = match.Groups["prefix"].Value;
                var value = match.Groups["value"].Value.Trim();

                if (
                    string.IsNullOrWhiteSpace(value)
                    || !CanonicalPrefixes.TryGetValue(prefix, out var action)
                )
                    return string.Empty;

                action(result, value);
                return string.Empty;
            }
        );

        if (string.IsNullOrWhiteSpace(leftover))
            return result;

        result.Other = leftover.Trim();

        return result;
    }
}
