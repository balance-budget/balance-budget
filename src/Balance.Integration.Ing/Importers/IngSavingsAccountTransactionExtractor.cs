using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.BankAccount;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngSavingsAccountTransactionExtractor : IBankTransactionExtractor
{
    private const string ImporterKey = "Ing.SavingsAccount.V1";

    public string Key => ImporterKey;
    public BankAccountType SupportedType => BankAccountType.Savings;

    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IIngSavingsAccountStatementParser _ingSavingsAccountStatementParser;

    public IngSavingsAccountTransactionExtractor(
        IIngSavingsAccountStatementParser ingSavingsAccountStatementParser
    )
    {
        _ingSavingsAccountStatementParser = ingSavingsAccountStatementParser;
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
                $"ING savings-account statements are in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        IReadOnlyList<SavingsAccountStatementRow> rows;
        try
        {
            rows = await _ingSavingsAccountStatementParser.ParseStatementsAsync(
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
        var firstAccount = NormalizeAccount(rows[0].Account);

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
            if (NormalizeAccount(row.Account) != firstAccount)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file mixes rows from multiple Accounts; all rows must share "
                        + "the same Account value."
                );
            }

            if (!string.IsNullOrWhiteSpace(row.Currency) && !IsEur(row.Currency))
            {
                return new InvariantError(
                    ErrorCodes.ImportCurrencyMismatch,
                    $"ING savings-account statements are in EUR; row dated "
                        + $"{row.Date:yyyy-MM-dd} is in '{row.Currency}'."
                );
            }
        }

        // ING CSVs are most-recent-first. Reverse so insertion order — and the time-ordered
        // BankTransaction.Id we mint per row — matches Date order; a list sorted by
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

    private static Result<BankTransaction> ToBankTransaction(
        BankAccountId bankAccountId,
        SavingsAccountStatementRow row
    )
    {
        // The savings 'Notifications' column is a plain free-text string (unlike the
        // current-account layout, which carries the structured ING note grammar). It is
        // surfaced as metadata only; the human-facing description comes from the
        // 'Description' column, which is required.
        var description = NullIfBlank(row.Description);
        if (description is null)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Row dated {row.Date:yyyy-MM-dd} has no usable description "
                    + "('Description' is blank)."
            );
        }

        var signedCents = ToSignedCents(row.Amount, row.DebitCredit);

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(signedCents, Eur),
            Description = description,
            // Savings rows are own-account transfers; the counterparty is your own current
            // account, resolved from its IBAN in the 'Counterparty' column, not by name.
            CounterpartyName = null,
            CounterpartyAccountNumber = NullIfBlank(row.CounterParty),
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ImporterKey = ImporterKey,
            Metadata = BuildMetadata(row),
        };
    }

    // Anything the extractor parses that is *not* promoted to a BankTransaction column lives
    // here (ADR 0009). The savings layout has no structured note, so only the two free-text
    // columns carry over.
    private static List<BankTransactionMetadataValue> BuildMetadata(SavingsAccountStatementRow row)
    {
        var entries = new List<BankTransactionMetadataValue>();
        AddString(entries, "Transaction Type", row.TransactionType);
        AddString(entries, "Notes", row.Notifications);
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

    private static bool IsEur(string currency) =>
        string.Equals(currency.Trim(), "EUR", StringComparison.OrdinalIgnoreCase);

    private static long ToSignedCents(decimal amount, DebitCredit debitCredit)
    {
        var cents = (long)decimal.Round(amount * 100m);
        return debitCredit is DebitCredit.Debit ? -cents : cents;
    }

    private static HashSet<string> OwnIdentifiers(BankAccount bankAccount)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(bankAccount.Iban))
            identifiers.Add(NormalizeAccount(bankAccount.Iban));
        if (!string.IsNullOrWhiteSpace(bankAccount.AccountNumber))
            identifiers.Add(NormalizeAccount(bankAccount.AccountNumber));
        return identifiers;
    }

    // ING savings statements quote the account number with separators (e.g. "D 595-90523");
    // the current-account side emits the same number contiguous (e.g. "D59590523"). Strip
    // spaces and dashes and uppercase so both forms compare equal — keeping the ADR 0012
    // self-transfer Attach symmetric across the current- and savings-account imports.
    private static string NormalizeAccount(string? value) =>
        value is null
            ? string.Empty
            : value
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .ToUpperInvariant();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
