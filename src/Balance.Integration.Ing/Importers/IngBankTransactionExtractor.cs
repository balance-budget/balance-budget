using System.Globalization;
using System.Reflection;
using System.Text;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.BankAccount;
using Balance.Integration.Ing.Models.Notes;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngBankTransactionExtractor : IBankTransactionExtractor
{
    private const string ImporterKey = "Ing.CurrentAccount";

    public string Key => ImporterKey;
    public string BankName => "ING";
    public BankAccountType SupportedType => BankAccountType.Current;

    private static readonly CurrencyCode Eur = new("EUR");

    private static readonly Dictionary<TransactionCode, string> TransactionCodeToString =
        typeof(TransactionCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToDictionary(
                f => (TransactionCode)f.GetValue(null)!,
                f => f.GetCustomAttribute<NameAttribute>()?.Names.FirstOrDefault() ?? f.Name
            );

    private readonly IIngCurrentAccountStatementParser _ingCurrentAccountStatementParser;
    private readonly IIngNoteParser _ingNoteParser;

    public IngBankTransactionExtractor(
        IIngCurrentAccountStatementParser ingCurrentAccountStatementParser,
        IIngNoteParser ingNoteParser
    )
    {
        _ingCurrentAccountStatementParser = ingCurrentAccountStatementParser;
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

        IReadOnlyList<CurrentAccountStatementRow> rows;
        try
        {
            rows = await _ingCurrentAccountStatementParser.ParseStatementsAsync(
                stream,
                cancellationToken
            );
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
        var firstAccount = Normalize(rows[0].Account);

        if (!ownIdentifiers.Contains(firstAccount))
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement Account '{rows[0].Account}' does not match this "
                    + "BankAccount's Iban or AccountNumber."
            );
        }

        foreach (var row in rows)
        {
            if (Normalize(row.Account) != firstAccount)
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
        CurrentAccountStatementRow row
    )
    {
        var note = _ingNoteParser.ParseNote(row.Notifications);

        var description = FirstNonBlank(note.Description, row.Description);
        if (description is null)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Row dated {row.Date:yyyy-MM-dd} has no usable description "
                    + "('Name / Description' is blank and 'Notifications' carries no "
                    + "'Description:' field)."
            );
        }

        var counterpartyName = NullIfBlank(row.Description);
        var counterpartyAccountNumber = ResolveCounterpartyAccountNumber(row, note);
        var signedCents = ToSignedCents(row.Amount, row.DebitCredit);

        var foreignAmountMinor = ToMinorUnits(note.ForeignCurrencyAmount?.Amount);
        var foreignCurrencyCode = NullIfBlank(note.ForeignCurrencyAmount?.CurrencyCode);
        var exchangeRate =
            note.ForeignCurrencyRate == 0m ? (decimal?)null : note.ForeignCurrencyRate;

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(signedCents, Eur),
            Description = description,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = counterpartyAccountNumber,
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ValueDate = note.ValueDate,
            Reference = NullIfBlank(note.Reference),
            MandateId = NullIfBlank(note.MandateId),
            SepaCreditorId = NullIfBlank(note.Creditor?.Id),
            ForeignAmount = foreignAmountMinor,
            ForeignCurrencyCode = foreignCurrencyCode,
            ExchangeRate = exchangeRate,
            ImporterKey = ImporterKey,
            Metadata = BuildMetadata(row, note),
        };
    }

    // Anything the extractor parses that is *not* promoted to a BankTransaction column lives
    // here (ADR 0009). Keys are global namespace; bank-prefixed only for genuinely
    // bank-specific extras. Nested values flatten with dotted keys.
    private static List<BankTransactionMetadataValue> BuildMetadata(
        CurrentAccountStatementRow row,
        IngNote note
    )
    {
        var entries = new List<BankTransactionMetadataValue>();

        // ING-specific fields
        AddString(entries, "Transaction Code", TransactionCodeToString[row.Code]);
        AddString(entries, "Transaction Type", row.TransactionType);
        AddString(entries, "Tags", row.Tag);

        // Notes
        AddString(entries, "Transaction", note.Transaction);
        AddString(entries, "Term", note.Term);

        if (
            note.ForeignCurrencyMarkUp is { } markUp
            && ToMinorUnits(note.ForeignCurrencyRate) is { } rate
            && ToMinorUnits(markUp.Amount) is { } markUpAmount
        )
        {
            AddInteger(entries, "Foreign Currency Rate", rate);
            AddInteger(entries, "Foreign Currency Mark Up Amount", markUpAmount);
            AddString(entries, "Foreign Currency Mark Up Code", markUp.CurrencyCode);
        }

        if (note.ForeignCurrencyFee is { } fee && ToMinorUnits(fee.Amount) is { } feeAmount)
        {
            AddInteger(entries, "Foreign Currency Fee Amount", feeAmount);
            AddString(entries, "Foreign Currency Fee Code", fee.CurrencyCode);
        }

        AddString(entries, "SEPA Description", note.Creditor?.Description);
        AddString(entries, "SEPA Other Party", note.OtherParty);

        // A row is either a card payment (carries a CardSequence) or a transfer (carries a
        // DateTime), never both — so exactly one "Date" metadata entry is written. Writing both
        // unconditionally would produce two values under the same key (there is no unique index on
        // (BankTransactionId, KeyId)); the else keeps the key single-valued.
        if (note.CardSequence is { } cardSequence)
        {
            // Card payment: use the card sequence for the exact date / time.
            AddString(entries, "Card Sequence Number", cardSequence.SequenceNumber);
            AddString(
                entries,
                "Date",
                cardSequence.DateTime.ToString("o", CultureInfo.InvariantCulture)
            );
        }
        else
        {
            // Transfer: use the date / time field.
            AddString(entries, "Date", note.DateTime?.ToString("o", CultureInfo.InvariantCulture));
        }

        AddString(entries, "Other Notes", note.Other);

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

    private static long? ToMinorUnits(decimal? amount)
    {
        if (amount is null)
            return null;

        // ING foreign amounts in 'Notifications' are quoted in the foreign currency's
        // major units. The promoted column carries minor units (paired with the
        // foreign currency code). We don't know the foreign currency's
        // MinorUnitScale here — Currency reference data lives in Balance.Data and
        // the extractor sits below that. The fixed-set columns assume scale 2 for
        // all practical SEPA-region currencies (USD/GBP/CHF/PLN/SEK/NOK etc.);
        // this matches what every other ING note value is parsed under.
        return (long)decimal.Round(amount.Value * 100m);
    }

    private static string? ResolveCounterpartyAccountNumber(
        CurrentAccountStatementRow row,
        IngNote note
    )
    {
        if (!string.IsNullOrWhiteSpace(row.CounterParty))
            return row.CounterParty;

        // ING own-savings transfers leave 'Counterparty' blank and embed the savings number
        // (D########) in 'Name / Description' or in the parsed note's free-text leftover.
        var pattern = IngPatterns.SavingsAccount();

        var inDescription = pattern.Match(row.Description);
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
            identifiers.Add(Normalize(bankAccount.Iban));
        if (!string.IsNullOrWhiteSpace(bankAccount.AccountNumber))
            identifiers.Add(Normalize(bankAccount.AccountNumber));
        return identifiers;
    }

    private static string Normalize(string? value) =>
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
