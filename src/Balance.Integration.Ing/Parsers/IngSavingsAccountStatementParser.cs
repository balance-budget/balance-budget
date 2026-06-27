using System.Globalization;
using System.Text;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper;
using CsvHelper.Configuration;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngSavingsAccountStatementParser : IIngSavingsAccountStatementParser
{
    public async ValueTask<IReadOnlyList<SavingsAccountStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        // leaveOpen: the caller owns the stream — detection probes it and then re-reads it for
        // the actual import (ADR 0034), so the parser must not dispose it.
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: -1,
            leaveOpen: true
        );
        using var csv = GetCsvReader(reader);

        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<SavingsAccountStatementRow>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = csv.GetRecord<SavingsAccountStatementRow>();
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
