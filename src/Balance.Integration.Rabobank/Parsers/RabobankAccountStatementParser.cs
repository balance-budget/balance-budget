using Balance.Integration.Rabobank.Contracts;
using Balance.Integration.Rabobank.Helpers;
using Balance.Integration.Rabobank.Models;

namespace Balance.Integration.Rabobank.Parsers;

internal sealed class RabobankAccountStatementParser : IRabobankAccountStatementParser
{
    public async ValueTask<IReadOnlyList<AccountStatementRow>?> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var content = await RabobankCsv.ReadAllAsync(stream, cancellationToken);
        if (!RabobankCsv.HeaderMentions(content, "IBAN/BBAN", "Volgnr", "Saldo na trn"))
            return null;

        using var reader = new StringReader(content);
        using var csv = RabobankCsv.CreateReader(reader);

        var rows = new List<AccountStatementRow>();
        while (await csv.ReadAsync(cancellationToken))
        {
            var parsed = csv.GetRecord<AccountStatementRow>();
            parsed.RawRecord = csv.Current.ToString();
            rows.Add(parsed);
        }
        return rows;
    }
}
