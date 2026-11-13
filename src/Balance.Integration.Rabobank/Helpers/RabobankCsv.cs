using System.Globalization;
using System.Text;
using CsvHelper;

namespace Balance.Integration.Rabobank.Helpers;

// Reading primitives shared by both Rabobank CSV layouts: Latin-1, comma-separated, every field
// quoted, and amounts like "+1.234,56".
internal static class RabobankCsv
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    private const NumberStyles AmountStyles =
        NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowThousands;

    // Reads the whole export, which is small, so the header can be checked before any row is
    // mapped. leaveOpen: the caller owns the stream, because detection probes it and then the
    // import re-reads it (ADR 0034).
    public static async ValueTask<string> ReadAllAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(
            stream,
            Encoding.Latin1,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: -1,
            leaveOpen: true
        );
        return await reader.ReadToEndAsync(cancellationToken);
    }

    // A layout is recognized by columns that only its own header carries.
    public static bool HeaderMentions(string content, params ReadOnlySpan<string> columns)
    {
        var lineEnd = content.IndexOfAny(['\r', '\n']);
        var header = lineEnd < 0 ? content : content[..lineEnd];
        foreach (var column in columns)
        {
            if (!header.Contains($"\"{column}\"", StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    public static CsvReader CreateReader(TextReader reader) =>
        CsvReader.Create(
            reader,
            new CsvReaderOptions
            {
                HasHeader = true,
                Separator = ',',
                // Bad quoting means this is not a valid Rabobank export: fail instead of
                // recovering, so format detection keeps rejecting foreign files.
                MalformedData = CsvMalformedDataHandling.Throw,
                FormatProvider = CultureInfo.InvariantCulture,
            },
            leaveOpen: true
        );

    public static decimal ParseAmount(string value) =>
        decimal.Parse(value.Trim(), AmountStyles, Dutch);

    public static decimal? ParseOptionalAmount(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseAmount(value);

    // Statement amounts are in the account currency's major units; the extractors only accept EUR
    // accounts, and foreign amounts assume scale 2, the same assumption the ING extractors make.
    public static long ToMinorUnits(decimal amount) => (long)decimal.Round(amount * 100m);

    public static long? ToMinorUnits(decimal? amount) =>
        amount is null ? null : ToMinorUnits(amount.Value);
}
