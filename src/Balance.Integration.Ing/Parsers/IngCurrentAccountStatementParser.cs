using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper;
using CsvHelper.Configuration;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngCurrentAccountStatementParser : IIngCurrentAccountStatementParser
{
    public async ValueTask<IReadOnlyList<CurrentAccountStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(stream);
        using var csv = GetCsvReader(reader);

        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<CurrentAccountStatementRow>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = csv.GetRecord<CurrentAccountStatementRow>();
            parsed.RawRecord = csv.Context.Parser?.RawRecord ?? string.Empty;
            rows.Add(parsed);
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
