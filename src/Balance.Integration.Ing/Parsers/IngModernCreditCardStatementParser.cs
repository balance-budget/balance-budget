using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Models.Notes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngModernCreditCardStatementParser : IIngCreditCardStatementParser
{
    private const double LineYTolerance = 0.5;

    private static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    private static readonly FrozenDictionary<string, CreditCardTransactionType> TransactionTypes =
        new Dictionary<string, CreditCardTransactionType>(StringComparer.Ordinal)
        {
            ["Betaling"] = CreditCardTransactionType.Payment,
            ["Ontvangst"] = CreditCardTransactionType.Receipt,
            ["Incasso"] = CreditCardTransactionType.DirectDebit,
            ["Geldopname"] = CreditCardTransactionType.CashWithdrawal,
            ["Kosten"] = CreditCardTransactionType.Fees,
            ["Correctie"] = CreditCardTransactionType.Correction,
        }.ToFrozenDictionary();

    public ValueTask<CreditCardStatement> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var lines = ExtractLines(stream, cancellationToken);
        var linkedAccount = FindLinkedAccountLine(lines);
        var rows = ParseRows(lines, cancellationToken);
        return ValueTask.FromResult(
            new CreditCardStatement { LinkedAccount = linkedAccount, Rows = rows }
        );
    }

    private static List<CreditCardStatementRow> ParseRows(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken
    )
    {
        var transactions = FindTransactionLines(lines).ToList();
        if (transactions.Count == 0)
            return [];

        var tableEnd = FindTableEnd(lines, transactions[^1].LineIndex);
        var rows = new List<CreditCardStatementRow>(transactions.Count);

        for (var i = 0; i < transactions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var noteStart = transactions[i].LineIndex + 1;
            var noteEnd = i + 1 < transactions.Count ? transactions[i + 1].LineIndex : tableEnd;
            var notes = ParseNotes(lines, noteStart, Math.Min(noteEnd, tableEnd));

            rows.Add(BuildRow(transactions[i], lines, notes));
        }

        return rows;
    }

    private static IEnumerable<MatchedLine> FindTransactionLines(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var match = IngPatterns.ModernCreditCardTransactionLine().Match(lines[i].Trim());
            if (match.Success)
                yield return new MatchedLine(i, match);
        }
    }

    private static string FindLinkedAccountLine(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = IngPatterns.CreditCardLinkedAccount().Match(line);
            if (!match.Success)
                continue;

            return match.Value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        }

        throw new InvalidOperationException("No counter-party line found");
    }

    private static CreditCardStatementRow BuildRow(
        MatchedLine transaction,
        IReadOnlyList<string> lines,
        ParsedNotes notes
    )
    {
        var transactionLine = lines[transaction.LineIndex];
        var bookingDate = ParseDate(transaction.Match.Groups["date"].Value);
        var notesText = string.Join('\n', notes.Lines);

        return new CreditCardStatementRow
        {
            Date = bookingDate,
            Description = transaction.Match.Groups["name"].Value.Trim(),
            TransactionType = TransactionTypes[transaction.Match.Groups["type"].Value],
            Amount = ParseAmount(transaction.Match.Groups["amount"].Value),
            CardNumber = notes.CardNumber ?? string.Empty,
            TransactionDate = notes.TransactionDate ?? bookingDate,
            ForeignCurrencyAmount = notes.ForeignCurrencyAmount,
            ForeignCurrencyRate = notes.ForeignCurrencyRate,
            ForeignCurrencyMarkUp = notes.ForeignCurrencyMarkUp,
            Notes = notesText,
            RawRecord =
                notes.Lines.Count == 0 ? transactionLine : $"{transactionLine}\n{notesText}",
        };
    }

    private static ParsedNotes ParseNotes(IReadOnlyList<string> lines, int start, int end)
    {
        var notes = new ParsedNotes();

        for (var i = start; i < end; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0)
                continue;

            var match = IngPatterns.ModernCreditCardNoteLine().Match(trimmed);
            if (!match.Success)
                continue;

            notes.Lines.Add(match.Value);
            ApplyNoteGroups(notes, match);
        }

        return notes;
    }

    private static void ApplyNoteGroups(ParsedNotes notes, Match match)
    {
        if (TryGroup(match, "date", out var date))
            notes.TransactionDate = ParseDate(date);

        if (TryGroup(match, "cardno", out var cardNumber))
            notes.CardNumber = cardNumber;

        if (
            TryGroup(match, "fcamount", out var fcAmount)
            && TryGroup(match, "fccode", out var fcCode)
        )
            notes.ForeignCurrencyAmount = CurrencyAmount.TryParse($"{fcAmount} {fcCode}");

        if (
            TryGroup(match, "fcrate", out var fcRate)
            && decimal.TryParse(fcRate, NumberStyles.Number, NlCulture, out var rate)
        )
            notes.ForeignCurrencyRate = rate;

        if (
            TryGroup(match, "fcmarkupamount", out var markUp)
            && TryGroup(match, "fcmarkupcode", out var markUpCode)
        )
            notes.ForeignCurrencyMarkUp = CurrencyAmount.TryParse($"{markUp} {markUpCode}");
    }

    private static bool TryGroup(Match match, string name, out string value)
    {
        var group = match.Groups[name];
        if (group.Success)
        {
            value = group.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static int FindTableEnd(IReadOnlyList<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            if (IngPatterns.ModernCreditCardFooter().IsMatch(lines[i]))
                return i;
        }
        return lines.Count;
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

    // Group words by Y (with tolerance) to reconstruct visual lines. PDF coordinates put
    // the origin at the bottom-left, so larger Y is higher on the page — order descending
    // for top-down, then ascending X within each line for left-to-right.
    private static IEnumerable<string> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture);

    // The transaction-line amount includes the leading +/- and may carry whitespace
    // between sign and digits (e.g. "+ 12,34"). Collapse the space so decimal.Parse with
    // nl-NL handles the sign and comma decimal mark in one shot.
    private static decimal ParseAmount(string captured) =>
        decimal.Parse(
            captured.Replace(" ", string.Empty, StringComparison.Ordinal),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            NlCulture
        );

    private readonly record struct MatchedLine(int LineIndex, Match Match);

    private sealed class ParsedNotes
    {
        public List<string> Lines { get; } = [];
        public string? CardNumber { get; set; }
        public DateOnly? TransactionDate { get; set; }
        public CurrencyAmount? ForeignCurrencyAmount { get; set; }
        public decimal? ForeignCurrencyRate { get; set; }
        public CurrencyAmount? ForeignCurrencyMarkUp { get; set; }
    }
}
