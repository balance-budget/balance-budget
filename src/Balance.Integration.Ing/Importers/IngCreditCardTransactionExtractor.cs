using System.Globalization;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.BankAccount;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Models.Notes;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngCreditCardTransactionExtractor : IBankTransactionExtractor
{
    private const string ImporterKey = "Ing.CreditCard.V1";

    public string Key => ImporterKey;
    public BankAccountType SupportedType => BankAccountType.Card;

    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IIngCreditCardStatementParser _ingCreditCardStatementParser;
    private readonly IIngNoteParser _ingNoteParser;

    public IngCreditCardTransactionExtractor(
        IIngCreditCardStatementParser ingCreditCardStatementParser,
        IIngNoteParser ingNoteParser
    )
    {
        _ingCreditCardStatementParser = ingCreditCardStatementParser;
        _ingNoteParser = ingNoteParser;
    }

    public async Task<Result<IReadOnlyList<BankTransaction>>> ExtractAsync(
        BankAccount bankAccount,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
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

        CreditCardStatement statement;
        try
        {
            statement = await _ingCreditCardStatementParser.ParseStatementsAsync(
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

        if (statement.Rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var firstCardNumber = Normalise(statement.Rows[0].CardNumber);
        if (Normalise(bankAccount.CardIdentifier) != firstCardNumber)
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement card number '{statement.Rows[0].CardNumber}' does not match this "
                    + "Card's CardIdentifier."
            );
        }

        foreach (var row in statement.Rows)
        {
            if (Normalise(row.CardNumber) != firstCardNumber)
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
        var bankTransactions = new List<BankTransaction>(statement.Rows.Count);
        foreach (var row in statement.Rows.Reverse())
        {
            var mapped = ToBankTransaction(bankAccount.Id, row, statement.Counterparty);
            if (mapped.IsFailure)
                return mapped.Error;
            bankTransactions.Add(mapped.Value);
        }
        return bankTransactions;
    }

    private static Result<BankTransaction> ToBankTransaction(
        BankAccountId bankAccountId,
        CreditCardStatementRow row,
        string fundingAccountIban
    )
    {
        var description = row.Description;
        var counterpartyName = NullIfBlank(row.Description);
        var signedCents = ToMinorUnits(row.Amount) ?? 0;

        // Only the DirectDebit (pay-down) row genuinely moves money from the funding current
        // account into the card; merchant rows (Payment, CashWithdrawal etc.) have the merchant
        // as the counterparty and no per-row counterparty IBAN. Populating the funding IBAN
        // here lets ADR 0013's Attach predicate fire on the card-side pay-down without
        // amending clause (3).
        var counterpartyAccountNumber =
            row.TransactionType is CreditCardTransactionType.DirectDebit
                ? NullIfBlank(fundingAccountIban)
                : null;

        var foreignAmountMinor = ToMinorUnits(row.ForeignCurrencyAmount?.Amount);
        var foreignCurrencyCode = NullIfBlank(row.ForeignCurrencyAmount?.CurrencyCode);
        var exchangeRate = row.ForeignCurrencyRate;

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(signedCents, Eur),
            Description = description,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = counterpartyAccountNumber,
            RawSource = RowHasher.Normalise(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ForeignAmount = foreignAmountMinor,
            ForeignCurrencyCode = foreignCurrencyCode,
            ExchangeRate = exchangeRate,
            ImporterKey = ImporterKey,
            Metadata = BuildMetadata(row),
        };
    }

    // Anything the extractor parses that is *not* promoted to a BankTransaction column lives
    // here (ADR 0015). Keys are global namespace; bank-prefixed only for genuinely
    // bank-specific extras. Nested values flatten with dotted keys.
    private static List<BankTransactionMetadataValue> BuildMetadata(CreditCardStatementRow row)
    {
        var entries = new List<BankTransactionMetadataValue>();

        // ING-specific fields
        AddString(entries, "Transaction Type", row.TransactionType.ToString());

        if (
            row.ForeignCurrencyMarkUp is { } markUp
            && ToMinorUnits(row.ForeignCurrencyRate) is { } rate
            && ToMinorUnits(markUp.Amount) is { } markUpAmount
        )
        {
            AddInteger(entries, "Foreign Currency Rate", rate);
            AddInteger(entries, "Foreign Currency Mark Up Amount", markUpAmount);
            AddString(entries, "Foreign Currency Mark Up Code", markUp.CurrencyCode);
        }

        // For transfers, use the date / time field
        AddString(entries, "Date", row.TransactionDate.ToString("o", CultureInfo.InvariantCulture));

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

    private static long? ToMinorUnits(decimal? amount) =>
        amount is null ? null : (long)decimal.Round(amount.Value * 100m);

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
