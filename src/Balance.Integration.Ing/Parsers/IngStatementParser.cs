using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Statements;
using CsvHelper;
using CsvHelper.Configuration;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngStatementParser : IIngStatementParser
{
    public async ValueTask<IReadOnlyList<CurrentAccountStatementRow>> ParseStatementsAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(path);
        using var csv = GetCsvReader(reader);

        return await csv.GetRecordsAsync<CurrentAccountStatementRow>(cancellationToken)
            .ToListAsync(cancellationToken);
    }

    private static CsvReader GetCsvReader(TextReader reader) =>
        new(
            reader,
            new CsvConfiguration(CultureInfo.GetCultureInfo("nl-NL"))
            {
                HasHeaderRecord = true,
                Delimiter = ";",
            }
        );
}
