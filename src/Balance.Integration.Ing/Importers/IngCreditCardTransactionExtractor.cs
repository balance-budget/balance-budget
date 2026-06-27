using System.Globalization;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;

namespace Balance.Integration.Ing.Importers;

// The single logical ING credit-card importer (ADR 0034). It reads the PDF once, then sniffs the
// concrete statement layout (legacy pre-2016 vs current) by content — never filename or date —
// and parses with the one matching layout. Both layouts map identically to BankTransactions.
internal sealed class IngCreditCardTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IReadOnlyList<IIngCreditCardStatementParser> _layouts;

    public IngCreditCardTransactionExtractor(IEnumerable<IIngCreditCardStatementParser> layouts) =>
        _layouts = layouts.ToList();

    public string Key => "Ing.CreditCard";
    public string BankName => "ING";
    public BankAccountType SupportedType => BankAccountType.Card;

    public Task<Result<IReadOnlyList<BankTransaction>>> ExtractAsync(
        BankAccount bankAccount,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        ArgumentNullException.ThrowIfNull(stream);
        return Task.FromResult(Extract(bankAccount, stream, cancellationToken));
    }

    private Result<IReadOnlyList<BankTransaction>> Extract(
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
                $"ING credit-card statements are in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        var lines = IngCreditCardPdfReader.ExtractLines(stream, cancellationToken);

        // Content sniffing: exactly one layout must recognize the file. None means an
        // unrecognized export; more than one would be a bug-class layout overlap. Either way we
        // fail loudly rather than guess (ADR 0034).
        var matching = _layouts.Where(layout => layout.CanParse(lines)).ToList();
        if (matching.Count != 1)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                "This file does not match a known ING credit-card statement layout."
            );
        }

        var layout = matching[0];
        CreditCardStatement statement;
        try
        {
            statement = layout.ParseStatement(lines, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to parse ING credit-card statement: {ex.Message}"
            );
        }

        if (statement.Rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var firstCardNumber = Normalize(statement.Rows[0].CardNumber);
        if (Normalize(bankAccount.CardIdentifier) != firstCardNumber)
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement card number '{statement.Rows[0].CardNumber}' does not match this "
                    + "Card's CardIdentifier."
            );
        }

        foreach (var row in statement.Rows)
        {
            if (Normalize(row.CardNumber) != firstCardNumber)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file mixes rows from multiple Accounts; all rows must share "
                        + "the same Account value."
                );
            }
        }

        // Some layouts list the most recent transaction first; reverse those so the time-ordered
        // BankTransaction.Id minted per row follows BookingDate.
        var ordered = layout.RowsAreMostRecentFirst
            ? statement.Rows.AsEnumerable().Reverse()
            : statement.Rows;

        var bankTransactions = new List<BankTransaction>(statement.Rows.Count);
        foreach (var row in ordered)
        {
            var mapped = ToBankTransaction(bankAccount.Id, row, statement.LinkedAccount);
            if (mapped.IsFailure)
                return mapped.Error;
            bankTransactions.Add(mapped.Value);
        }
        return bankTransactions;
    }

    public Task<ImportIdentity?> TryIdentifyAsync(
        ImportFile file,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        // Credit-card statements are PDFs with no account anchor in the filename; the card number
        // only lives in the content. Skip non-PDF drops cheaply rather than letting the PDF
        // reader throw, then sniff the layout and anchor on the first row's card number.
        if (!IngAnchor.LooksLikePdf(file.Content))
            return Task.FromResult<ImportIdentity?>(null);

        var lines = IngCreditCardPdfReader.ExtractLines(file.Content, cancellationToken);
        if (file.Content.CanSeek)
            file.Content.Seek(0, SeekOrigin.Begin);

        var layout = _layouts.FirstOrDefault(candidate => candidate.CanParse(lines));
        if (layout is null)
            return Task.FromResult<ImportIdentity?>(null);

        CreditCardStatement statement;
        try
        {
            statement = layout.ParseStatement(lines, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult<ImportIdentity?>(null);
        }

        var identity =
            statement.Rows.Count == 0
                ? null
                : new ImportIdentity(Key, SupportedType, Normalize(statement.Rows[0].CardNumber));
        return Task.FromResult(identity);
    }

    private Result<BankTransaction> ToBankTransaction(
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
        // here lets ADR 0012's Attach predicate fire on the card-side pay-down without
        // amending clause (3).
        var counterpartyAccountNumber = row.TransactionType
            is CreditCardTransactionType.DirectDebit
                or CreditCardTransactionType.Repayment
                or CreditCardTransactionType.Correction
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
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ForeignAmount = foreignAmountMinor,
            ForeignCurrencyCode = foreignCurrencyCode,
            ExchangeRate = exchangeRate,
            ImporterKey = Key,
            Metadata = BuildMetadata(row),
        };
    }

    // Anything the extractor parses that is *not* promoted to a BankTransaction column lives
    // here (ADR 0009). Keys are global namespace; bank-prefixed only for genuinely
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

    private static string Normalize(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
