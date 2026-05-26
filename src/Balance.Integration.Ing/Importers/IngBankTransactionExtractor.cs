using System.Globalization;
using System.Reflection;
using System.Text;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.Notes;
using Balance.Integration.Ing.Models.Statements;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngBankTransactionExtractor
    : IBankTransactionExtractor,
        IBankTransactionReExtractor
{
    internal const string Key = "Ing.CurrentAccount.V1";

    // CSV header used to re-parse a single stored RawSource row (issue #89 backfill).
    // CurrentAccountStatementRow tolerates both English and Dutch headers; we stick to Dutch
    // because that's what every real ING export and every stored RawSource uses today.
    private const string CsvHeader =
        "\"Datum\";\"Naam / Omschrijving\";\"Rekening\";\"Tegenrekening\";\"Code\";"
        + "\"Af Bij\";\"Bedrag (EUR)\";\"Mutatiesoort\";\"Mededelingen\";"
        + "\"Saldo na mutatie\";\"Tag\"";

    string IBankTransactionReExtractor.ImporterKey => Key;

    private static readonly CurrencyCode Eur = new("EUR");

    private static readonly Dictionary<TransactionCode, string> TransactionCodeToString =
        typeof(TransactionCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToDictionary(
                f => (TransactionCode)f.GetValue(null)!,
                f => f.GetCustomAttribute<NameAttribute>()?.Names.FirstOrDefault() ?? f.Name
            );

    private readonly IIngStatementParser _ingStatementParser;
    private readonly IIngNoteParser _ingNoteParser;

    public IngBankTransactionExtractor(
        IIngStatementParser ingStatementParser,
        IIngNoteParser ingNoteParser
    )
    {
        _ingStatementParser = ingStatementParser;
        _ingNoteParser = ingNoteParser;
    }

    public async Task<Result<IReadOnlyList<BankTransaction>>> ExtractAsync(
        BankAccount bankAccount,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        ArgumentNullException.ThrowIfNull(stream);

        if (bankAccount.AccountId is null)
        {
            return new InvariantError(
                ErrorCodes.ImportBankAccountNotOwned,
                "BankTransactions can only be imported onto one of your own BankAccounts "
                    + "(BankAccount.AccountId must be set)."
            );
        }

        if (bankAccount.CurrencyCode != Eur)
        {
            return new InvariantError(
                ErrorCodes.ImportCurrencyMismatch,
                $"ING current-account statements are in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        IReadOnlyList<IngStatementRow> rows;
        try
        {
            rows = await _ingStatementParser.ParseStatementsAsync(stream, cancellationToken);
        }
        catch (CsvHelperException ex)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to parse ING statement CSV: {ex.Message}"
            );
        }

        if (rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var ownIdentifiers = OwnIdentifiers(bankAccount);
        var firstAccount = Normalise(rows[0].Parsed.Account);

        if (!ownIdentifiers.Contains(firstAccount))
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement Account '{rows[0].Parsed.Account}' does not match this "
                    + "BankAccount's Iban or AccountNumber."
            );
        }

        foreach (var row in rows)
        {
            if (Normalise(row.Parsed.Account) != firstAccount)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file mixes rows from multiple Accounts; all rows must share "
                        + "the same Account value."
                );
            }
        }

        // ING CSVs are most-recent-first. Reverse so insertion order — and the time-ordered
        // BankTransaction.Id we mint per row — matches BookingDate order; a list sorted by
        // (BookingDate, Id) then breaks intra-day ties in CSV-chronological order.
        var bankTransactions = new List<BankTransaction>(rows.Count);
        foreach (var row in rows.Reverse())
        {
            var mapped = ToBankTransaction(bankAccount.Id, row);
            if (mapped.IsFailure)
                return mapped.Error;
            bankTransactions.Add(mapped.Value);
        }
        return bankTransactions;
    }

    private Result<BankTransaction> ToBankTransaction(
        BankAccountId bankAccountId,
        IngStatementRow row
    )
    {
        var note = _ingNoteParser.ParseNote(row.Parsed.Notifications);

        var description = FirstNonBlank(note.Description, row.Parsed.Description);
        if (description is null)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Row dated {row.Parsed.Date:yyyy-MM-dd} has no usable description "
                    + "('Naam / Omschrijving' is blank and 'Mededelingen' carries no "
                    + "'Omschrijving:' field)."
            );
        }

        var counterpartyName = NullIfBlank(row.Parsed.Description);
        var counterpartyAccountNumber = ResolveCounterpartyAccountNumber(row, note);
        var signedCents = ToSignedCents(row.Parsed.Amount, row.Parsed.DebitCredit);

        var foreignAmountMinor = ToMinorUnits(note.ForeignCurrencyAmount);
        var foreignCurrencyCode = NullIfBlank(note.ForeignCurrencyAmount?.CurrencyCode);
        var exchangeRate =
            note.ForeignCurrencyRate == 0m ? (decimal?)null : note.ForeignCurrencyRate;

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Parsed.Date,
            Money = new Money(signedCents, Eur),
            Description = description,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = counterpartyAccountNumber,
            RawSource = RowHasher.Normalise(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ValueDate = note.ValueDate,
            Reference = NullIfBlank(note.Reference),
            MandateId = NullIfBlank(note.MandateId),
            SepaCreditorId = NullIfBlank(note.Creditor?.Id),
            ForeignAmount = foreignAmountMinor,
            ForeignCurrencyCode = foreignCurrencyCode,
            ExchangeRate = exchangeRate,
            ImporterKey = Key,
            Metadata = BuildMetadata(row, note),
        };
    }

    // Re-extracts the promoted columns + metadata from a stored RawSource row (issue #89
    // backfill). Re-parses the row with the same CSV + note parsers and the same per-row
    // mapping as the live import — only the surrounding "load BankAccount + dedup" plumbing
    // and the (already-stable) identifying columns are skipped.
    async Task<Result<BankTransactionReExtraction>> IBankTransactionReExtractor.ReExtractAsync(
        string rawSource,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(rawSource);

        // RawSource is one normalised CSV data row; CsvHelper needs a header to bind columns.
        // Prepend the known ING header so the same parser used by ExtractAsync handles it.
        var synthetic = CsvHeader + "\n" + rawSource + "\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(synthetic));

        IReadOnlyList<IngStatementRow> rows;
        try
        {
            rows = await _ingStatementParser.ParseStatementsAsync(stream, cancellationToken);
        }
        catch (CsvHelperException ex)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to re-parse stored RawSource as an ING row: {ex.Message}"
            );
        }

        if (rows.Count == 0)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                "Stored RawSource yielded no CSV row when re-parsed."
            );
        }

        var row = rows[0];
        var note = _ingNoteParser.ParseNote(row.Parsed.Notifications);
        return MapPromotedAndMetadata(row, note);
    }

    private static BankTransactionReExtraction MapPromotedAndMetadata(
        IngStatementRow row,
        IngNote note
    )
    {
        var foreignAmountMinor = ToMinorUnits(note.ForeignCurrencyAmount);
        var foreignCurrencyCode = NullIfBlank(note.ForeignCurrencyAmount?.CurrencyCode);
        var exchangeRate =
            note.ForeignCurrencyRate == 0m ? (decimal?)null : note.ForeignCurrencyRate;

        return new BankTransactionReExtraction(
            ValueDate: note.ValueDate,
            Reference: NullIfBlank(note.Reference),
            MandateId: NullIfBlank(note.MandateId),
            SepaCreditorId: NullIfBlank(note.Creditor?.Id),
            ForeignAmount: foreignAmountMinor,
            ForeignCurrencyCode: foreignCurrencyCode,
            ExchangeRate: exchangeRate,
            Metadata: BuildMetadata(row, note)
        );
    }

    // Anything the extractor parses that is *not* promoted to a BankTransaction column lives
    // here (ADR 0015). Keys are global namespace; bank-prefixed only for genuinely
    // bank-specific extras. Nested values flatten with dotted keys.
    private static List<BankTransactionMetadataValue> BuildMetadata(
        IngStatementRow row,
        IngNote note
    )
    {
        var entries = new List<BankTransactionMetadataValue>();

        AddString(entries, "IngTransactionCode", TransactionCodeToString[row.Parsed.Code]);
        AddString(entries, "IngMutatiesoort", row.Parsed.TransactionType);
        AddString(entries, "IngTag", row.Parsed.Tag);
        AddString(entries, "SepaCreditorName", note.Creditor?.Description);
        AddString(entries, "OtherParty", note.OtherParty);
        AddString(entries, "Term", note.Term);

        if (
            note.CardSequence is { } cardSequence
            && int.TryParse(
                cardSequence.SequenceNumber,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sequence
            )
        )
        {
            AddInteger(entries, "CardSequence.Number", sequence);
        }

        if (note.ForeignCurrencyMarkUp is { } markUp)
        {
            AddInteger(entries, "ForeignMarkUp.Amount", ToMinorUnits(markUp)!.Value);
            AddString(entries, "ForeignMarkUp.CurrencyCode", markUp.CurrencyCode);
        }

        if (note.ForeignCurrencyFee is { } fee)
        {
            AddInteger(entries, "ForeignFee.Amount", ToMinorUnits(fee)!.Value);
            AddString(entries, "ForeignFee.CurrencyCode", fee.CurrencyCode);
        }

        AddString(entries, "IngNote.Other", note.Other);

        return entries;
    }

    private static void AddString(
        List<BankTransactionMetadataValue> entries,
        string keyName,
        string? value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        entries.Add(
            new BankTransactionMetadataValue
            {
                Key = new BankTransactionMetadataKey { Name = keyName },
                StringValue = value,
            }
        );
    }

    private static void AddInteger(
        List<BankTransactionMetadataValue> entries,
        string keyName,
        long value
    ) =>
        entries.Add(
            new BankTransactionMetadataValue
            {
                Key = new BankTransactionMetadataKey { Name = keyName },
                IntegerValue = value,
            }
        );

    private static long? ToMinorUnits(CurrencyAmount? amount)
    {
        if (amount is null)
            return null;
        // ING foreign amounts in 'Mededelingen' are quoted in the foreign currency's
        // major units. The promoted column carries minor units (paired with the
        // foreign currency code). We don't know the foreign currency's
        // MinorUnitScale here — Currency reference data lives in Balance.Data and
        // the extractor sits below that. The fixed-set columns assume scale 2 for
        // all practical SEPA-region currencies (USD/GBP/CHF/PLN/SEK/NOK etc.);
        // this matches what every other ING note value is parsed under.
        return (long)decimal.Round(amount.Amount * 100m);
    }

    private static string? ResolveCounterpartyAccountNumber(IngStatementRow row, IngNote note)
    {
        if (!string.IsNullOrWhiteSpace(row.Parsed.CounterParty))
            return row.Parsed.CounterParty;

        // ING own-savings transfers leave 'Tegenrekening' blank and embed the savings number
        // (D########) in 'Naam / Omschrijving' or in the parsed note's free-text leftover.
        var pattern = IngPatterns.SavingsAccountPattern();

        var inDescription = pattern.Match(row.Parsed.Description);
        if (inDescription.Success)
            return inDescription.Value;

        if (!string.IsNullOrWhiteSpace(note.Other))
        {
            var inNoteOther = pattern.Match(note.Other);
            if (inNoteOther.Success)
                return inNoteOther.Value;
        }

        return null;
    }

    private static long ToSignedCents(decimal amount, DebitCredit debitCredit)
    {
        var cents = (long)decimal.Round(amount * 100m);
        return debitCredit is DebitCredit.Debit ? -cents : cents;
    }

    private static HashSet<string> OwnIdentifiers(BankAccount bankAccount)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(bankAccount.Iban))
            identifiers.Add(Normalise(bankAccount.Iban));
        if (!string.IsNullOrWhiteSpace(bankAccount.AccountNumber))
            identifiers.Add(Normalise(bankAccount.AccountNumber));
        return identifiers;
    }

    private static string Normalise(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static string? FirstNonBlank(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
            return a;
        if (!string.IsNullOrWhiteSpace(b))
            return b;
        return null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
