using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Statements;
using CsvHelper;
using CsvHelper.Configuration;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngCurrentAccountStatementParser : IIngCurrentAccountStatementParser
{
    public async ValueTask<IReadOnlyList<IngStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(stream);
        using var csv = GetCsvReader(reader);

        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<IngStatementRow>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = csv.GetRecord<CurrentAccountStatementRow>();
            var rawRecord = csv.Context.Parser?.RawRecord ?? string.Empty;
            rows.Add(new IngStatementRow(parsed, rawRecord));
        }
        return rows;
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
