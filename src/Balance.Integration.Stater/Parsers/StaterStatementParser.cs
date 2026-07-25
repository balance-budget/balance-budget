using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Balance.Integration.Pdf;
using Balance.Integration.Stater.Contracts;
using Balance.Integration.Stater.Models;

namespace Balance.Integration.Stater.Parsers;

// Layout: a "Nummer: x.x.x" header carrying the account number (= the loan number), then a table
// with columns Datum | Bedrag | Betaald aan | Omschrijving. Rows are reconstructed from word
// positions: the column a word belongs to follows from the header labels, a line carrying a date in
// the Datum column starts a row, and any line close enough below it is a wrapped cell of that row.
internal sealed partial class StaterStatementParser : IStaterStatementParser
{
    private const int DateColumn = 0;
    private const int AmountColumn = 1;
    private const int CounterpartyColumn = 2;
    private const int DescriptionColumn = 3;

    // A wrapped cell sits on the next visual line, about one line height below. Anything further
    // down has left the table (page footer, closing letter), so it ends the row instead.
    private const double MaxWrapGapInLineHeights = 3;

    public StaterStatement? Parse(IReadOnlyList<PdfTextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var accountNumber = FindAccountNumber(lines);
        return accountNumber is null ? null : new StaterStatement(accountNumber, ParseRows(lines));
    }

    private static string? FindAccountNumber(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (var line in lines)
        {
            var match = HeaderAccountRegex().Match(line.Text);
            if (match.Success)
                return match.Groups["acct"].Value;
        }
        return null;
    }

    private static List<StaterStatementRow> ParseRows(IReadOnlyList<PdfTextLine> lines)
    {
        var header = FindTableHeader(lines);
        if (header is null)
            return [];

        var (headerIndex, columns) = header.Value;

        var rows = new List<StaterStatementRow>();
        Row? current = null;
        var previous = lines[headerIndex];

        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (ClosingSectionRegex().IsMatch(line.Text))
                break;

            var words = columns.WordsInTable(line);
            if (words.Count == 0)
                continue;

            var startsRow = columns.StartsRow(words);
            if (startsRow)
            {
                Close(rows, current);
                current = new Row();
            }
            else if (current is null || !IsWrapOf(previous, line))
            {
                Close(rows, current);
                current = null;
                previous = line;
                continue;
            }

            current.AddLine(line.Text);
            foreach (var word in words)
                current.AddWord(columns.ColumnOf(word), word.Text, isWrap: !startsRow);
            previous = line;
        }

        Close(rows, current);
        return rows;
    }

    // A row is only kept once complete: a date and an amount are what make it a transaction.
    private static void Close(List<StaterStatementRow> rows, Row? current)
    {
        if (current?.ToStatementRow() is { } row)
            rows.Add(row);
    }

    // Wrapped cells keep the table's leading; a jump to the page footer or the closing letter does
    // not. Baselines are only comparable within a page.
    private static bool IsWrapOf(PdfTextLine previous, PdfTextLine line) =>
        previous.PageNumber == line.PageNumber
        && previous.Baseline - line.Baseline
            <= MaxWrapGapInLineHeights * Math.Max(previous.Height, line.Height);

    private static (int Index, TableColumns Columns)? FindTableHeader(
        IReadOnlyList<PdfTextLine> lines
    )
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (
                TransactionHeaderRegex().IsMatch(lines[i].Text)
                && BuildColumns(lines[i]) is { } columns
            )
            {
                return (i, columns);
            }
        }
        return null;
    }

    // Column anchors are the header labels' centers, so a word belongs to the column whose anchor
    // is nearest and the boundaries sit midway between adjacent anchors.
    private static TableColumns? BuildColumns(PdfTextLine header)
    {
        if (
            CenterOf(header, "Datum") is not { } datum
            || CenterOf(header, "Bedrag") is not { } bedrag
            || CenterOf(header, "Betaald") is not { } betaald
            || CenterOf(header, "Omschrijving") is not { } omschrijving
        )
        {
            return null;
        }

        return new TableColumns(
            header.Words[0].Left,
            [(datum + bedrag) / 2, (bedrag + betaald) / 2, (betaald + omschrijving) / 2]
        );
    }

    private static double? CenterOf(PdfTextLine line, string label)
    {
        foreach (var word in line.Words)
        {
            if (word.Text.Equals(label, StringComparison.OrdinalIgnoreCase))
                return word.CenterX;
        }
        return null;
    }

    private sealed class TableColumns
    {
        private readonly double _left;
        private readonly double[] _boundaries;

        public TableColumns(double left, double[] boundaries)
        {
            _left = left;
            _boundaries = boundaries;
        }

        // Anything entirely left of the first column is page furniture (Stater prints a rotated
        // form code down the margin), not table content.
        public IReadOnlyList<PdfWord> WordsInTable(PdfTextLine line) =>
            [.. line.Words.Where(word => word.Right >= _left)];

        public bool StartsRow(IReadOnlyList<PdfWord> words) =>
            words.Any(word => ColumnOf(word) == DateColumn && DateRegex().IsMatch(word.Text));

        public int ColumnOf(PdfWord word)
        {
            var column = 0;
            while (column < _boundaries.Length && word.CenterX >= _boundaries[column])
                column++;
            return column;
        }
    }

    private sealed class Row
    {
        private readonly StringBuilder[] _cells = [new(), new(), new(), new()];
        private readonly StringBuilder _raw = new();

        public void AddLine(string text)
        {
            if (_raw.Length > 0)
                _raw.Append('\n');
            _raw.Append(text);
        }

        // A wrapped line only extends cells the row's first line already filled: a word that drifts
        // into an empty column band must not be invented as that column's value.
        public void AddWord(int column, string text, bool isWrap)
        {
            var cell = _cells[column];
            if (isWrap && cell.Length == 0)
                return;

            // A wrap that broke a word after '/' or '-' rejoins without a space; otherwise the
            // fragments are separate words and take a space.
            if (cell.Length > 0 && cell[^1] is not ('/' or '-'))
                cell.Append(' ');
            cell.Append(text);
        }

        public StaterStatementRow? ToStatementRow()
        {
            if (!TryParseDate(Cell(DateColumn), out var date))
                return null;
            if (!TryParseAmount(Cell(AmountColumn), out var amount))
                return null;

            var counterparty = Cell(CounterpartyColumn);
            return new StaterStatementRow(
                date,
                Cell(DescriptionColumn),
                counterparty.Length == 0 ? null : counterparty,
                amount,
                _raw.ToString()
            );
        }

        private string Cell(int column) => _cells[column].ToString().Trim();
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            "dd-MM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );

    // "." thousands separator, "," decimal separator (two places).
    private static bool TryParseAmount(string value, out long minorUnits)
    {
        minorUnits = 0;
        var normalized = value
            .Replace(".", "", StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);
        if (
            !decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var major
            )
        )
            return false;

        minorUnits = (long)decimal.Round(major * 100m);
        return true;
    }

    [GeneratedRegex(@"(?:Nummer)\s*:?\s*(?<acct>[0-9]+\.[0-9]+\.[0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderAccountRegex();

    [GeneratedRegex(
        @"^Datum bij– of(afschrijving)?\s+Bedrag\s+Betaald aan\s+Omschrijving",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex TransactionHeaderRegex();

    [GeneratedRegex(@"^\d{2}-\d{2}-\d{4}$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^Heeft u nog vragen\?", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingSectionRegex();
}
