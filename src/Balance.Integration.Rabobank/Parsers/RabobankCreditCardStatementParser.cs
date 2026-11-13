using Balance.Integration.Rabobank.Contracts;
using Balance.Integration.Rabobank.Helpers;
using Balance.Integration.Rabobank.Models;

namespace Balance.Integration.Rabobank.Parsers;

internal sealed class RabobankCreditCardStatementParser : IRabobankCreditCardStatementParser
{
    public async ValueTask<IReadOnlyList<CreditCardStatementRow>?> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var content = await RabobankCsv.ReadAllAsync(stream, cancellationToken);
        if (!RabobankCsv.HeaderMentions(content, "Creditcard Nummer", "Tegenrekening IBAN"))
            return null;

        using var reader = new StringReader(content);
        using var csv = RabobankCsv.CreateReader(reader);

        var rows = new List<CreditCardStatementRow>();
        while (await csv.ReadAsync(cancellationToken))
        {
            var parsed = csv.GetRecord<CreditCardStatementRow>();
            parsed.RawRecord = csv.Current.ToString();
            rows.Add(parsed);
        }
        return rows;
    }
}
