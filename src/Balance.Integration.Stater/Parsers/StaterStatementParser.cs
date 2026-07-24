using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Balance.Integration.Stater.Contracts;
using Balance.Integration.Stater.Models;

namespace Balance.Integration.Stater.Parsers;

// Layout: a "Nummer: x.x.x" header carrying the account number (= the loan number), then a table
// with columns Datum | Bedrag | Betaald aan | Omschrijving. Both the header and the Omschrijving
// cell can wrap onto a second visual line, so rows are reconstructed from word x-positions: the
// column an x falls in is fixed by the header labels, and a line whose Datum column holds no date
// is a continuation whose words append to the row above.
internal sealed partial class StaterStatementParser : IStaterStatementParser
{
    private enum Column
    {
        Date = 0,
        Amount = 1,
        Counterparty = 2,
        Description = 3,
    }

    public StaterStatement? Parse(IReadOnlyList<StaterTextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var accountNumber = FindAccountNumber(lines);
        if (accountNumber is null)
            return null;

        var rows = ParseRows(lines);
        return new StaterStatement(accountNumber, rows);
    }

    private static string? FindAccountNumber(IReadOnlyList<StaterTextLine> lines)
    {
        foreach (var line in lines)
        {
            var match = HeaderAccountRegex().Match(line.Text);
            if (match.Success)
                return match.Groups["acct"].Value;
        }
        return null;
    }

    private static List<StaterStatementRow> ParseRows(IReadOnlyList<StaterTextLine> lines)
    {
        var rows = new List<StaterStatementRow>();

        double[]? boundaries = null;
        RowBuilder? current = null;

        void Flush()
        {
            if (current?.ToRow() is { } row)
                rows.Add(row);
            current = null;
        }

        var inTable = false;
        foreach (var line in lines)
        {
            if (PageFooterRegex().IsMatch(line.Text) || line.Text.Length <= 1)
                continue;

            if (!inTable)
            {
                if (
                    TransactionHeaderRegex().IsMatch(line.Text)
                    && TryColumnBoundaries(line, out var b)
                )
                {
                    boundaries = b;
                    inTable = true;
                }
                continue;
            }

            if (ClosingSectionRegex().IsMatch(line.Text))
                break;

            var startsRow = StartsNewRow(line, boundaries!);
            if (startsRow)
            {
                Flush();
                current = new RowBuilder(line.Text);
            }
            else if (current is null)
            {
                continue;
            }
            else
            {
                current.AppendRaw(line.Text);
            }

            foreach (var word in line.Words)
                current!.Add(ColumnOf(word.Center, boundaries!), word.Text);
        }

        Flush();
        return rows;
    }

    // Column anchors are the header labels' centers; a word falls in the column whose center is
    // nearest (boundaries sit midway between adjacent anchors).
    private static bool TryColumnBoundaries(StaterTextLine header, out double[] boundaries)
    {
        boundaries = [];
        if (
            Center(header, "Datum") is not { } datum
            || Center(header, "Bedrag") is not { } bedrag
            || Center(header, "Betaald") is not { } betaald
            || Center(header, "Omschrijving") is not { } omschrijving
        )
        {
            return false;
        }

        boundaries = [(datum + bedrag) / 2, (bedrag + betaald) / 2, (betaald + omschrijving) / 2];
        return true;
    }

    private static double? Center(StaterTextLine line, string label)
    {
        foreach (var word in line.Words)
        {
            if (word.Text.Equals(label, StringComparison.OrdinalIgnoreCase))
                return word.Center;
        }
        return null;
    }

    private static Column ColumnOf(double center, double[] boundaries)
    {
        var index = 0;
        foreach (var boundary in boundaries)
        {
            if (center < boundary)
                break;
            index++;
        }
        return (Column)index;
    }

    private static bool StartsNewRow(StaterTextLine line, double[] boundaries)
    {
        foreach (var word in line.Words)
        {
            if (ColumnOf(word.Center, boundaries) == Column.Date && DateRegex().IsMatch(word.Text))
                return true;
        }
        return false;
    }

    private sealed class RowBuilder
    {
        private readonly StringBuilder[] _cells =
        [
            new StringBuilder(),
            new StringBuilder(),
            new StringBuilder(),
            new StringBuilder(),
        ];
        private readonly StringBuilder _raw = new();

        public RowBuilder(string rawFirstLine) => _raw.Append(rawFirstLine);

        public void AppendRaw(string rawLine) => _raw.Append('\n').Append(rawLine);

        public void Add(Column column, string text)
        {
            var cell = _cells[(int)column];
            // A wrap that broke a word after '/' or '-' rejoins without a space; otherwise the
            // fragments are separate words and take a space.
            if (cell.Length > 0 && cell[^1] is not ('/' or '-'))
                cell.Append(' ');
            cell.Append(text);
        }

        public StaterStatementRow? ToRow()
        {
            if (!TryParseDate(_cells[(int)Column.Date].ToString().Trim(), out var date))
                return null;
            if (!TryParseAmount(_cells[(int)Column.Amount].ToString().Trim(), out var amount))
                return null;

            var counterparty = _cells[(int)Column.Counterparty].ToString().Trim();
            var description = _cells[(int)Column.Description].ToString().Trim();

            return new StaterStatementRow(
                date,
                description,
                counterparty.Length == 0 ? null : counterparty,
                amount,
                _raw.ToString()
            );
        }
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

    [GeneratedRegex(
        "^(Lloyds Bank GmbH|Bestuurders|Nederlandse vestiging)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex PageFooterRegex();
}
