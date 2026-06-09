using System.Text.RegularExpressions;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngLegacyCreditCardStatementParser : IngCreditCardStatementParser
{
    protected override CreditCardStatement ParseStatement(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken
    )
    {
        var header = ParseHeader(lines);
        return new CreditCardStatement
        {
            LinkedAccount = header.LinkedAccount,
            Rows = ParseRows(lines, header, cancellationToken),
        };
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
                linkedAccount = NormalizeLinkedAccount(linkedAccountMatch.Value);
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
                repaymentDate = ParseDate(repaymentDateMatch.Groups["date"].Value);
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
                RawRecord = $"{header.RepaymentDate.Year}|{match.Value}",
            };
        }

        var bookingDate = ParseLineDate(match.Groups["date"].Value, header.RepaymentDate);
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
            RawRecord = $"{bookingDate.Year}|{match.Value}",
        };
    }

    private static DateOnly ParseLineDate(string value, DateOnly repaymentDate)
    {
        var parsed = DateOnly.ParseExact(value, "dd MMM", NlCulture);

        // The line date is just "dd MMM" and lacks a year, so we need to use the year of the repayment date
        var year = repaymentDate.Year;

        // Statement from jan will have transactions from the previous year
        if (repaymentDate.Month == 1 && parsed.Month == 12)
            year--;

        return new DateOnly(year, parsed.Month, parsed.Day);
    }

    private readonly record struct Header(
        string LinkedAccount,
        string CardNumber,
        DateOnly RepaymentDate
    );
}
