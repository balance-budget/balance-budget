using System.Text.RegularExpressions;

namespace Balance.Integration.Ing.Parsers;

// ING packs a run of "Prefix: value" pairs into one statement column with no escaping, so a value
// ends only where the next known prefix begins. This scanner is that grammar, once: it matches each
// pair with a lookahead to the next prefix, hands the value to the configured setter, and reports
// whatever text belonged to no prefix at all.
//
// The prefix vocabulary differs per statement layout (and per export language), and so do the
// number and date cultures, so a scanner is built per dialect rather than shared across layouts.
internal sealed class IngNotePrefixScanner<TNote>
{
    private readonly Dictionary<string, Action<TNote, string>> _setters;
    private readonly Regex _pattern;
    private readonly char[] _valueTrim;

    /// <param name="setters">Prefix (as it appears in the export) to the field it fills.</param>
    /// <param name="valueTrim">
    /// Extra characters to strip from a captured value's end, for dialects that delimit pairs with
    /// punctuation the lookahead leaves behind (the credit-card note is comma-separated).
    /// </param>
    public IngNotePrefixScanner(
        IEnumerable<KeyValuePair<string, Action<TNote, string>>> setters,
        params char[] valueTrim
    )
    {
        ArgumentNullException.ThrowIfNull(setters);
        _setters = new Dictionary<string, Action<TNote, string>>(setters, StringComparer.Ordinal);
        _valueTrim = valueTrim;

        // Longest prefix first so an alternation never settles for a prefix that is the start of a
        // longer one ("Transaction" vs "Transaction date").
        var prefixes = string.Join(
            "|",
            _setters.Keys.OrderByDescending(prefix => prefix.Length).Select(Regex.Escape)
        );
        _pattern = new Regex(
            $"(?<prefix>{prefixes}): (?<value>.*?)(?=(?:{prefixes}):|$)",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );
    }

    /// <summary>
    /// Applies every recognized prefix in <paramref name="note"/> to <paramref name="target"/> and
    /// returns the leftover text, or null when everything was consumed.
    /// </summary>
    public string? Scan(string note, TNote target)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var leftover = _pattern.Replace(
            note,
            match =>
            {
                var prefix = match.Groups["prefix"].Value;
                var value = Trim(match.Groups["value"].Value);

                if (value.Length == 0 || !_setters.TryGetValue(prefix, out var setter))
                    return string.Empty;

                setter(target, value);
                return string.Empty;
            }
        );

        return string.IsNullOrWhiteSpace(leftover) ? null : Trim(leftover);
    }

    private string Trim(string value) =>
        _valueTrim.Length == 0 ? value.Trim() : value.Trim().TrimEnd(_valueTrim).Trim();
}
