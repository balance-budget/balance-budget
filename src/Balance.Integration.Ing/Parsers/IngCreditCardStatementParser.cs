using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.CreditCard;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Parsers;

internal sealed partial class IngCreditCardStatementParser : IIngCreditCardStatementParser
{
    private const double LineYTolerance = 0.5;

    private static readonly string[] NoteWhitelistPrefixes =
    [
        "Transactiedatum:",
        "Kaartnummer:",
        "Bedrag:",
        "Koers:",
        "Koersopslag",
    ];

    [GeneratedRegex(
        @"^(?<date>\d{2}-\d{2}-\d{4})\s+(?<name>.+?)\s+(?<type>Betaling|Ontvangst|Incasso|Geldopname|Kosten|Correctie)\s+(?<sign>[+-])\s*(?<amount>\d[\d.,]*\d{2})$"
    )]
    private static partial Regex TransactionLinePattern();

    [GeneratedRegex(@"Geboekt op Naam", RegexOptions.IgnoreCase)]
    private static partial Regex FooterGeboektOpNaam();

    [GeneratedRegex(@"Overeenkomstnummer", RegexOptions.IgnoreCase)]
    private static partial Regex FooterOvereenkomstnummer();

    [GeneratedRegex(@"Dit product valt", RegexOptions.IgnoreCase)]
    private static partial Regex FooterDitProductValt();

    [GeneratedRegex(@"Op ING\.nl", RegexOptions.IgnoreCase)]
    private static partial Regex FooterOpIngNl();

    public ValueTask<CreditCardStatement> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var rows = Parse(stream, cancellationToken);
        return new ValueTask<CreditCardStatement>(
            new CreditCardStatement { Account = "", Rows = rows }
        );
    }

    private static IReadOnlyList<CreditCardStatementRow> Parse(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var lines = ExtractLines(stream, cancellationToken);

        var matches = new List<(int Index, Match Match)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var match = TransactionLinePattern().Match(lines[i].Trim());
            if (match.Success)
                matches.Add((i, match));
        }

        if (matches.Count == 0)
            return Array.Empty<CreditCardStatementRow>();

        var tableEnd = FindTableEnd(lines, matches[^1].Index);
        var rows = new List<CreditCardStatementRow>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (lineIndex, match) = matches[i];
            var nextIndex = i + 1 < matches.Count ? matches[i + 1].Index : tableEnd;
            var endIndex = Math.Min(nextIndex, tableEnd);

            var noteLines = new List<string>();
            for (var j = lineIndex + 1; j < endIndex; j++)
            {
                var stripped = lines[j].Trim();
                if (stripped.Length == 0)
                    continue;
                if (!IsNoteLine(stripped))
                    continue;
                noteLines.Add(stripped);
            }
            var notes = string.Join('\n', noteLines);

            var date = DateOnly.ParseExact(
                match.Groups["date"].Value,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture
            );
            var amount = NormaliseAmount(
                match.Groups["sign"].ValueSpan,
                match.Groups["amount"].Value
            );

            var rawRecord =
                noteLines.Count == 0
                    ? lines[lineIndex]
                    : string.Concat(lines[lineIndex], "\n", notes);

            var parsed = new CreditCardStatementRow
            {
                CardNumber = "",
                Date = date,
                Description = match.Groups["name"].Value.Trim(),
                TransactionType = MapTransactionType(match.Groups["type"].Value),
                Amount = amount,
                Notes = notes,
                RawRecord = rawRecord,
            };

            rows.Add(parsed);
        }

        return rows;
    }

    private static List<string> ExtractLines(Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.AddRange(ExtractPageLines(page));
        }

        return lines;
    }

    private static IEnumerable<string> ExtractPageLines(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            return Array.Empty<string>();

        // Group words by Y position (with tolerance) to reconstruct visual lines, then
        // order top-down (PDF coordinates put origin at the bottom-left, so larger Y
        // is higher on the page) and left-to-right within each line.
        return words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .ToList();
    }

    private static int FindTableEnd(List<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            var text = lines[i];
            if (
                FooterGeboektOpNaam().IsMatch(text)
                || FooterOvereenkomstnummer().IsMatch(text)
                || FooterDitProductValt().IsMatch(text)
                || FooterOpIngNl().IsMatch(text)
            )
                return i;
        }
        return lines.Count;
    }

    private static bool IsNoteLine(string line)
    {
        foreach (var prefix in NoteWhitelistPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static decimal NormaliseAmount(ReadOnlySpan<char> sign, string value)
    {
        // ING credit card statements quote amounts in nl-NL: '.' is the thousands
        // separator and ',' is the decimal mark. Strip thousands, swap decimal, then
        // parse invariantly so we don't depend on the host culture.
        var canonical = value
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');
        var amount = decimal.Parse(canonical, CultureInfo.InvariantCulture);
        return sign.Length > 0 && sign[0] == '-' ? -amount : amount;
    }

    private static CreditCardTransactionType MapTransactionType(string type) =>
        type switch
        {
            "Betaling" => CreditCardTransactionType.Payment,
            "Ontvangst" => CreditCardTransactionType.Receipt,
            "Incasso" => CreditCardTransactionType.DirectDebit,
            "Geldopname" => CreditCardTransactionType.CashWithdrawal,
            "Kosten" => CreditCardTransactionType.Fees,
            "Correctie" => CreditCardTransactionType.Correction,
            _ => throw new InvalidOperationException(
                $"Unrecognised ING credit-card transaction type '{type}'."
            ),
        };
}
