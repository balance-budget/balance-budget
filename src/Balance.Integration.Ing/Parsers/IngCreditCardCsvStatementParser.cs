using System.Collections.Frozen;
using System.Text;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.BankAccount;
using Balance.Integration.Ing.Models.CreditCard;
using CsvHelper;
using CsvHelper.Configuration;

namespace Balance.Integration.Ing.Parsers;

// ING's credit-card CSV export — a third Statement layout under the same logical importer as the
// two PDF layouts (ADR 0038). The Dutch and English exports are dialects of this one layout,
// detected from the header row because the export switches number culture with the language.
internal sealed class IngCreditCardCsvStatementParser : IIngCreditCardStatementParser
{
    // Dutch is the authoritative vocabulary: it maps 1:1 onto the labels the PDF layouts already
    // use. English collapses Incasso and Ontvangst into one "Payment" label, so it is resolved
    // below rather than here.
    private static readonly FrozenDictionary<string, CreditCardTransactionType> DutchTypes =
        new Dictionary<string, CreditCardTransactionType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Betaling"] = CreditCardTransactionType.Payment,
            ["Ontvangst"] = CreditCardTransactionType.Receipt,
            ["Aflossing"] = CreditCardTransactionType.Repayment,
            ["Incasso"] = CreditCardTransactionType.DirectDebit,
            ["Geldopname"] = CreditCardTransactionType.CashWithdrawal,
            ["Kosten"] = CreditCardTransactionType.Fees,
            ["Correctie"] = CreditCardTransactionType.Correction,
            ["Diversen"] = CreditCardTransactionType.Miscellaneous,
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, CreditCardTransactionType> EnglishTypes =
        new Dictionary<string, CreditCardTransactionType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Debit"] = CreditCardTransactionType.Payment,
            ["Miscellaneous"] = CreditCardTransactionType.Miscellaneous,
            ["Cash withdrawal"] = CreditCardTransactionType.CashWithdrawal,
            ["Costs"] = CreditCardTransactionType.Fees,
            ["Correction"] = CreditCardTransactionType.Correction,
        }.ToFrozenDictionary();

    // The CSV lists the most recent transaction first.
    public bool RowsAreMostRecentFirst => true;

    public async ValueTask<bool> CanParseAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.LooksLikePdf)
            return false;

        return await DetectDialectAsync(source.Stream, cancellationToken) is not null;
    }

    public async ValueTask<CreditCardStatement> ParseStatementAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        var dialect =
            await DetectDialectAsync(source.Stream, cancellationToken)
            ?? throw new InvalidOperationException(
                "This file does not carry an ING credit-card CSV header."
            );

        return new CreditCardStatement
        {
            // The CSV never names the funding account; the extractor falls back to the Card's
            // configured Funding account.
            LinkedAccount = null,
            Rows = await ParseRowsAsync(source.Stream, dialect, cancellationToken),
        };
    }

    private static async ValueTask<List<CreditCardStatementRow>> ParseRowsAsync(
        Stream stream,
        CreditCardCsvDialect dialect,
        CancellationToken cancellationToken
    )
    {
        using var reader = NewReader(stream);
        using var csv = new CsvReader(
            reader,
            new CsvConfiguration(dialect.Culture()) { HasHeaderRecord = true, Delimiter = ";" }
        );

        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<CreditCardStatementRow>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = csv.GetRecord<CreditCardCsvRow>();
            var rawRecord = (csv.Context.Parser?.RawRecord ?? string.Empty).TrimEnd();
            rows.Add(BuildRow(parsed, dialect, Disambiguate(rawRecord, occurrences)));
        }

        return rows;
    }

    // Unlike the current- and savings-account exports, the credit-card CSV has no running-balance
    // column, so two identical charges on one day produce byte-identical rows — and dedup on
    // (BankAccountId, RowHash) would silently swallow the second as a duplicate. Mark the second and
    // later occurrences so they hash distinctly (ADR 0038). The marker is stable across re-exports
    // (it counts occurrences, not line numbers) and goes into RawSource too, so a hash stays
    // recomputable from what we stored. A quoted CSV row always starts with '"', so the marker can
    // never be mistaken for bank content.
    private static string Disambiguate(string rawRecord, Dictionary<string, int> occurrences)
    {
        var occurrence = occurrences.TryGetValue(rawRecord, out var seen) ? seen + 1 : 1;
        occurrences[rawRecord] = occurrence;
        return occurrence == 1 ? rawRecord : $"{occurrence}|{rawRecord}";
    }

    private static CreditCardStatementRow BuildRow(
        CreditCardCsvRow parsed,
        CreditCardCsvDialect dialect,
        string rawRecord
    )
    {
        var note = IngCreditCardCsvNoteParser.ParseNote(parsed.Notifications, dialect);
        var hasCardNumber = !string.IsNullOrWhiteSpace(parsed.CardNumber);

        return new CreditCardStatementRow
        {
            Date = parsed.Date,
            Description = parsed.Description.Trim(),
            CardNumber = parsed.CardNumber.Trim(),
            TransactionType = ResolveType(parsed.TransactionType, dialect, hasCardNumber),
            Amount = parsed.DebitCredit is DebitCredit.Debit ? -parsed.Amount : parsed.Amount,
            TransactionDate = note.TransactionDate ?? parsed.Date,
            ForeignCurrencyAmount = note.ForeignCurrencyAmount,
            ForeignCurrencyRate = note.ForeignCurrencyRate,
            ForeignCurrencyMarkUp = note.ForeignCurrencyMarkUp,
            Notes = parsed.Notifications.Trim(),
            RawRecord = rawRecord,
        };
    }

    private static CreditCardTransactionType ResolveType(
        string label,
        CreditCardCsvDialect dialect,
        bool hasCardNumber
    )
    {
        var trimmed = label.Trim();

        if (dialect is CreditCardCsvDialect.Dutch)
            return DutchTypes.GetValueOrDefault(trimmed, CreditCardTransactionType.Unknown);

        // English "Payment" covers both Incasso and Ontvangst. Only a row that names no card moves
        // money between the card and its Funding account, which separates the two exactly as the
        // Dutch export labels them.
        if (string.Equals(trimmed, "Payment", StringComparison.OrdinalIgnoreCase))
        {
            return hasCardNumber
                ? CreditCardTransactionType.Receipt
                : CreditCardTransactionType.DirectDebit;
        }

        return EnglishTypes.GetValueOrDefault(trimmed, CreditCardTransactionType.Unknown);
    }

    // Both dialects' header rows, matched on the columns that are unmistakably this layout's: a
    // card number alongside a transaction type, which no other ING export pairs.
    private static async ValueTask<CreditCardCsvDialect?> DetectDialectAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = NewReader(stream);
        var header = await reader.ReadLineAsync(cancellationToken);
        if (header is null)
            return null;

        if (Mentions(header, "Kaartnummer") && Mentions(header, "Mutatiesoort"))
            return CreditCardCsvDialect.Dutch;

        if (Mentions(header, "Card number") && Mentions(header, "Transaction type"))
            return CreditCardCsvDialect.English;

        return null;
    }

    private static bool Mentions(string header, string column) =>
        header.Contains($"\"{column}\"", StringComparison.OrdinalIgnoreCase);

    // leaveOpen: the caller owns the stream — detection probes it and then re-reads it for the
    // actual import (ADR 0034), and the dialect probe re-reads it before parsing.
    private static StreamReader NewReader(Stream stream) =>
        new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: -1,
            leaveOpen: true
        );
}
