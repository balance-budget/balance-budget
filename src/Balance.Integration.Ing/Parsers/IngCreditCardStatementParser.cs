using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngCreditCardStatementParser : IIngCreditCardStatementParser
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("nl-NL");
    private const double LineYTolerance = 0.5;

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
            var match = IngPatterns.CreditCardTransactionLine().Match(lines[i].Trim());
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

                var noteMatch = IngPatterns.CreditCardNoteLine().Match(stripped);
                if (!noteMatch.Success)
                    continue;

                noteLines.Add(noteMatch.Value);
            }
            var notes = string.Join('\n', noteLines);

            var date = DateOnly.ParseExact(
                match.Groups["date"].Value,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture
            );

            var amount = decimal.Parse(match.Groups["amount"].Value, Culture);

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
            if (IngPatterns.CreditCardFooter().IsMatch(text))
                return i;
        }
        return lines.Count;
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
            _ => throw new UnreachableException(
                $"Unrecognised ING credit-card transaction type '{type}'."
            ),
        };
}
