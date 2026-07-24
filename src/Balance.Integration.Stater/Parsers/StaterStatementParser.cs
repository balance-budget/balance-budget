using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Stater.Contracts;
using Balance.Integration.Stater.Models;

namespace Balance.Integration.Stater.Parsers;

// Assumed layout (to be corrected against a real statement):
//   - A header line labeling the account number (= the loan number, not an IBAN).
//   - Rows shaped "<dd-MM-yyyy> <description> [<paid-to marker> <name>] <amount> <direction>",
//     amount Dutch-formatted ("." thousands, "," decimal), direction "Bij" (in) or "Af" (out).
// The header labels, paid-to marker, and direction tokens are literals from the statement text.
internal sealed partial class StaterStatementParser : IStaterStatementParser
{
    public StaterStatement? Parse(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        string? accountNumber = null;
        foreach (var line in lines)
        {
            var header = HeaderAccountRegex().Match(line);
            if (header.Success)
            {
                accountNumber = header.Groups["acct"].Value;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(accountNumber))
            return null;

        var rows = new List<StaterStatementRow>();
        var matchingTransactions = false;
        foreach (var line in lines)
        {
            // Ignore page footer and single character lines (side margin numbers)
            if (PageFooterRegex().IsMatch(line) || line.Length == 1)
                continue;

            // We detected a header, any following lines should be transaction rows
            if (TransactionHeaderRegex().IsMatch(line))
            {
                matchingTransactions = true;
                continue;
            }

            if (!matchingTransactions)
                continue;

            // Closing section, stop parsing
            if (ClosingSectionRegex().IsMatch(line))
                break;

            var row = ParseRow(line);
            if (row is not null)
                rows.Add(row);
        }

        return new StaterStatement(accountNumber, rows);
    }

    private static StaterStatementRow? ParseRow(string line)
    {
        var match = TransactionRowRegex().Match(line);
        if (!match.Success)
            return null;

        if (!TryParseDate(match.Groups["date"].Value, out var date))
            return null;
        if (!TryParseAmount(match.Groups["amount"].Value, out var amount))
            return null;

        var counterparty = match.Groups["to"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();

        return new StaterStatementRow(date, description, counterparty, amount, line.Trim());
    }

    private static (string Description, string? Counterparty) SplitBody(string body)
    {
        /*
        var marker = PaidToRegex().Match(body);
        if (!marker.Success)
            return (body, null);

        var description = body[..marker.Index].Trim();
        var counterparty = body[(marker.Index + marker.Length)..].Trim();
        return (
            description.Length == 0 ? body : description,
            counterparty.Length == 0 ? null : counterparty
        );*/
        return ("", "");
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
        {
            return false;
        }

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

    //01-07-2026 -2.614,77 Lloyds Bank Verrekening i.v.m. maandbedrag/
    [GeneratedRegex(
        @"^(?<date>\d{2}-\d{2}-\d{4})\s+(?<amount>[\-\.\,0-9]+)\s((?<to>.+?)\s+)?((?<description>.+?)?)$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex TransactionRowRegex();

    [GeneratedRegex(@"^Heeft u nog vragen\?", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingSectionRegex();

    [GeneratedRegex(
        "^(Lloyds Bank GmbH|Bestuurders|Nederlandse vestiging)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex PageFooterRegex();
}
