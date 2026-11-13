using System.Collections.Frozen;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Rabobank.Contracts;
using Balance.Integration.Rabobank.Helpers;
using Balance.Integration.Rabobank.Models;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Rabobank.Importers;

// Rabobank current and savings accounts export the same CSV layout, so both Importers share this
// extractor and differ only in their key and BankAccountType.
internal abstract class RabobankAccountTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

    // Card payments and ATM withdrawals, at home and abroad. Their 'Omschrijving' columns hold the
    // terminal location and authorization details, so the merchant name is the better description.
    private static readonly FrozenSet<string> CardCodes = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "bc",
        "ba",
        "ga",
        "gb"
    );

    // A transfer between the current account and one of your Rabobank credit cards.
    private const string CreditCardCode = "cc";

    private readonly IRabobankAccountStatementParser _parser;

    protected RabobankAccountTransactionExtractor(IRabobankAccountStatementParser parser)
    {
        _parser = parser;
    }

    public abstract string Key { get; }
    public string BankName => "Rabobank";
    public abstract BankAccountType SupportedType { get; }

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
                "Rabobank account statements are imported in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        IReadOnlyList<AccountStatementRow>? rows;
        try
        {
            rows = await _parser.ParseStatementsAsync(stream, cancellationToken);
        }
        catch (Exception ex) when (ex is CsvFormatException or KeyNotFoundException)
        {
            return FormatInvalid(ex.Message);
        }

        if (rows is null)
            return FormatInvalid("the file does not carry a Rabobank account statement header.");

        if (rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var ownIdentifiers = OwnIdentifiers(bankAccount);
        var statementIban = RabobankAnchor.Normalize(rows[0].Iban);
        if (!ownIdentifiers.Contains(statementIban))
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement account '{rows[0].Iban}' does not match this BankAccount's Iban or "
                    + "AccountNumber."
            );
        }

        var bankTransactions = new List<BankTransaction>(rows.Count);
        foreach (var row in rows)
        {
            if (RabobankAnchor.Normalize(row.Iban) != statementIban)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file mixes rows from multiple accounts. Export one account per "
                        + "file."
                );
            }

            if (!string.Equals(row.Currency.Trim(), Eur.Value, StringComparison.OrdinalIgnoreCase))
            {
                return new InvariantError(
                    ErrorCodes.ImportCurrencyMismatch,
                    $"Rabobank account statements are imported in EUR; row dated "
                        + $"{row.Date:yyyy-MM-dd} is in '{row.Currency}'."
                );
            }

            Result<BankTransaction> mapped;
            try
            {
                mapped = ToBankTransaction(bankAccount.Id, statementIban, row);
            }
            catch (FormatException ex)
            {
                return FormatInvalid($"row dated {row.Date:yyyy-MM-dd}: {ex.Message}");
            }

            if (mapped.IsFailure)
                return mapped.Error;
            bankTransactions.Add(mapped.Value);
        }
        return bankTransactions;
    }

    public async Task<ImportIdentity?> TryIdentifyAsync(
        ImportFile file,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        var fromFilename = RabobankAnchor.FromAccountFilename(file.FileName);
        if (fromFilename is not null)
            return new ImportIdentity(Key, SupportedType, fromFilename);

        if (!file.Content.CanSeek)
            return null;

        file.Content.Seek(0, SeekOrigin.Begin);
        IReadOnlyList<AccountStatementRow>? rows;
        try
        {
            rows = await _parser.ParseStatementsAsync(file.Content, cancellationToken);
        }
        catch (Exception ex) when (ex is CsvFormatException or KeyNotFoundException)
        {
            return null;
        }
        finally
        {
            file.Content.Seek(0, SeekOrigin.Begin);
        }

        return rows is null || rows.Count == 0
            ? null
            : new ImportIdentity(Key, SupportedType, RabobankAnchor.Normalize(rows[0].Iban));
    }

    private Result<BankTransaction> ToBankTransaction(
        BankAccountId bankAccountId,
        string statementIban,
        AccountStatementRow row
    )
    {
        var isCardRow = CardCodes.Contains(row.Code.Trim());
        var counterpartyName = NullIfBlank(row.CounterpartyName);
        var description = isCardRow ? counterpartyName : JoinDescription(row) ?? counterpartyName;

        if (description is null)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Row dated {row.Date:yyyy-MM-dd} has no usable description ('Omschrijving' and "
                    + "'Naam tegenpartij' are blank)."
            );
        }

        // Bank fees name the account itself as the counterparty. A row can never be a transfer to
        // its own account, so that IBAN must not make the row look like a Self-transfer.
        var counterpartyIban = NullIfBlank(row.CounterpartyIban);
        if (RabobankAnchor.Normalize(counterpartyIban) == statementIban)
            counterpartyIban = null;

        var foreignAmount = RabobankCsv.ParseOptionalAmount(row.ForeignAmount);

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(RabobankCsv.ToMinorUnits(RabobankCsv.ParseAmount(row.Amount)), Eur),
            Description = description,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = counterpartyIban,
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ValueDate = row.ValueDate,
            MandateId = NullIfBlank(row.MandateId),
            SepaCreditorId = NullIfBlank(row.CreditorId),
            ForeignAmount = RabobankCsv.ToMinorUnits(foreignAmount),
            ForeignCurrencyCode = foreignAmount is null ? null : NullIfBlank(row.ForeignCurrency),
            ExchangeRate = RabobankCsv.ParseOptionalAmount(row.ExchangeRate),
            ImporterKey = Key,
            Metadata = BuildMetadata(row, isCardRow),
        };
    }

    private static List<BankTransactionMetadataValue> BuildMetadata(
        AccountStatementRow row,
        bool isCardRow
    )
    {
        var metadata = new MetadataBuilder()
            .AddString("Rabobank Code", row.Code)
            .AddString("Rabobank Volgnr", row.SequenceNumber)
            .AddString("Rabobank Transaction Reference", row.TransactionReference)
            .AddString("Rabobank Batch ID", row.BatchId)
            .AddString("Rabobank Payment Reference", row.PaymentReference)
            .AddString("Ultimate Party", row.UltimateParty)
            .AddString("Initiating Party", row.InitiatingParty)
            .AddString("Counterparty BIC", row.CounterpartyBic)
            .AddString("Return Reason", row.ReturnReason);

        if (isCardRow)
        {
            metadata
                .AddString("Card Location", row.Description1)
                .AddString("Card Terminal", row.Description2.Trim().TrimStart('.'));
        }

        if (string.Equals(row.Code.Trim(), CreditCardCode, StringComparison.OrdinalIgnoreCase))
        {
            var masked = RabobankPatterns.MaskedCardNumber().Match(row.Description1);
            if (masked.Success)
            {
                metadata.AddString(
                    "Card Identifier",
                    RabobankAnchor.CardIdentifier(masked.Groups["last4"].Value)
                );
            }
        }

        return metadata.Build();
    }

    private static string? JoinDescription(AccountStatementRow row)
    {
        var joined = string.Join(' ', row.Description1, row.Description2, row.Description3);
        var collapsed = RabobankPatterns.Whitespace().Replace(joined, " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    private static HashSet<string> OwnIdentifiers(BankAccount bankAccount)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(bankAccount.Iban))
            identifiers.Add(RabobankAnchor.Normalize(bankAccount.Iban));
        if (!string.IsNullOrWhiteSpace(bankAccount.AccountNumber))
            identifiers.Add(RabobankAnchor.Normalize(bankAccount.AccountNumber));
        return identifiers;
    }

    private static InvariantError FormatInvalid(string detail) =>
        new(ErrorCodes.ImportFormatInvalid, $"Failed to parse Rabobank statement CSV: {detail}");

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
