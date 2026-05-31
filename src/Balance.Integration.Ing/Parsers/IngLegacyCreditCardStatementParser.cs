using System.Globalization;
using System.Text.RegularExpressions;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Models.Notes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngLegacyCreditCardStatementParser : IIngCreditCardStatementParser
{
    private const double LineYTolerance = 0.5;

    private static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    public ValueTask<CreditCardStatement> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var lines = ExtractLines(stream, cancellationToken);
        var header = ParseHeader(lines);
        var rows = ParseRows(lines, header, cancellationToken);
        return ValueTask.FromResult(
            new CreditCardStatement { LinkedAccount = header.LinkedAccount, Rows = rows }
        );
    }

    private static List<CreditCardStatementRow> ParseRows(
        IReadOnlyList<string> lines,
        Header header,
        CancellationToken cancellationToken
    )
    {
        var transactions = FindTransactionLines(lines).ToList();
        if (transactions.Count == 0)
            return [];

        var rows = new List<CreditCardStatementRow>(transactions.Count);

        foreach (var match in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(BuildRow(header, match));
        }

        return rows;
    }

    private static IEnumerable<Match> FindTransactionLines(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = IngPatterns.LegacyCreditCardTransactionLine().Match(line.Trim());
            if (match.Success)
                yield return match;
        }
    }

    private static Header ParseHeader(IReadOnlyList<string> lines)
    {
        var linkedAccount = "";
        var cardNumber = "";
        DateOnly repaymentDate = default;

        foreach (var line in lines)
        {
            var linkedAccountMatch = IngPatterns.CreditCardLinkedAccount().Match(line);
            if (linkedAccountMatch.Success)
            {
                linkedAccount = linkedAccountMatch
                    .Value.Replace(" ", "", StringComparison.Ordinal)
                    .ToUpperInvariant();
            }

            var cardNumberMatch = IngPatterns.LegacyCreditCardNumberLine().Match(line);
            if (cardNumberMatch.Success)
            {
                cardNumber = cardNumberMatch
                    .Groups["cardno"]
                    .Value.Trim()
                    .Replace(".", "", StringComparison.Ordinal);
            }

            var repaymentDateMatch = IngPatterns.LegacyCreditCardRepaymentDateLine().Match(line);
            if (repaymentDateMatch.Success)
            {
                repaymentDate = ParseHaderDate(repaymentDateMatch.Groups["date"].Value);
            }
        }

        if (
            !string.IsNullOrWhiteSpace(linkedAccount)
            && !string.IsNullOrWhiteSpace(cardNumber)
            && repaymentDate != default
        )
            return new Header(linkedAccount, cardNumber, repaymentDate);

        throw new InvalidOperationException(
            "No linked account, card number or repayment date header found"
        );
    }

    private static CreditCardStatementRow BuildRow(Header header, Match match)
    {
        var description = match.Groups["desc"].Value.Trim();
        var debitCredit = match.Groups["dc"].Value.Trim();
        var amount = ParseAmount(match.Groups["amount"].Value);

        if (description == "AFLOSSING" && debitCredit == "BIJ")
        {
            return new CreditCardStatementRow
            {
                Date = header.RepaymentDate,
                CardNumber = header.CardNumber,
                Description = description,
                TransactionType = CreditCardTransactionType.DirectDebit,
                Amount = amount,
                Notes = "",
                RawRecord = match.Value,
            };
        }

        var bookingDate = ParseLineDate(match.Groups["date"].Value);
        CurrencyAmount? foreignCurrencyAmount = null;
        decimal? foreignCurrencyRate = null;

        if (match.Groups["fc"].Success)
        {
            foreignCurrencyAmount = new CurrencyAmount(
                ParseAmount(match.Groups["fcamount"].Value),
                match.Groups["fccode"].Value
            );
            foreignCurrencyRate = ParseAmount(match.Groups["fcrate"].Value);
        }

        return new CreditCardStatementRow
        {
            Date = bookingDate,
            CardNumber = header.CardNumber,
            Description = description,
            TransactionType = CreditCardTransactionType.Payment,
            Amount = (debitCredit == "AF" ? -1 : 1) * amount,
            ForeignCurrencyAmount = foreignCurrencyAmount,
            ForeignCurrencyRate = foreignCurrencyRate,
            TransactionDate = bookingDate,
            Notes = "",
            RawRecord = match.Value,
        };
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

    private static DateOnly ParseHaderDate(string value) =>
        DateOnly.ParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static DateOnly ParseLineDate(string value) =>
        DateOnly.ParseExact(value, "dd MMM", NlCulture);

    // The transaction-line amount includes the leading +/- and may carry whitespace
    // between sign and digits (e.g. "+ 12,34"). Collapse the space so decimal.Parse with
    // nl-NL handles the sign and comma decimal mark in one shot.
    private static decimal ParseAmount(string captured) =>
        decimal.Parse(
            captured.Replace(" ", string.Empty, StringComparison.Ordinal),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            NlCulture
        );

    private readonly record struct Header(
        string LinkedAccount,
        string CardNumber,
        DateOnly RepaymentDate
    );
}
