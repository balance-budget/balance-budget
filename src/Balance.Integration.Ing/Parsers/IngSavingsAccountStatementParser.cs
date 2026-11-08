using System.Globalization;
using System.Text;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper;

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

        var rows = new List<SavingsAccountStatementRow>();
        while (await csv.ReadAsync(cancellationToken))
        {
            var parsed = csv.GetRecord<SavingsAccountStatementRow>();
            parsed.RawRecord = csv.Current.ToString();
            rows.Add(parsed);
        }
        return rows;
    }

    private static CsvReader GetCsvReader(TextReader reader) =>
        CsvReader.Create(
            reader,
            new CsvReaderOptions
            {
                HasHeader = true,
                Separator = ';',
                // Bad quoting means this is not a valid ING export: fail instead of recovering,
                // so format detection keeps rejecting foreign files.
                MalformedData = CsvMalformedDataHandling.Throw,
                FormatProvider = CultureInfo.GetCultureInfo("nl-NL"),
            },
            leaveOpen: true
        );
}
