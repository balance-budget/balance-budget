using System.Globalization;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Ing.Importers;

// The single logical ING credit-card importer (ADR 0034). It sniffs the concrete statement layout
// (legacy PDF, current PDF, or CSV export) by content — never filename or date — and parses with
// the one matching layout. Every layout maps identically to BankTransactions.
internal sealed class IngCreditCardTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IReadOnlyList<IIngCreditCardStatementParser> _layouts;

    public IngCreditCardTransactionExtractor(IEnumerable<IIngCreditCardStatementParser> layouts) =>
        _layouts = layouts.ToList();

    public string Key => "Ing.CreditCard";
    public string BankName => "ING";
    public BankAccountType SupportedType => BankAccountType.Card;

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
                $"ING credit-card statements are in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        var source = new IngCreditCardStatementSource(stream);

        // Content sniffing: exactly one layout must recognize the file. None means an
        // unrecognized export; more than one would be a bug-class layout overlap. Either way we
        // fail loudly rather than guess (ADR 0034).
        var matching = new List<IIngCreditCardStatementParser>();
        foreach (var candidate in _layouts)
        {
            if (await candidate.CanParseAsync(source, cancellationToken))
                matching.Add(candidate);
        }

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
            statement = await layout.ParseStatementAsync(source, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to parse ING credit-card statement: {ex.Message}"
            );
        }
        catch (CsvHelperException ex)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to parse ING credit-card statement CSV: {ex.Message}"
            );
        }

        if (statement.Rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var cardCheck = EnsureCardMatches(bankAccount, statement);
        if (cardCheck.IsFailure)
            return cardCheck.Error;

        var fundingResult = ResolveFundingAccount(bankAccount, statement);
        if (fundingResult.IsFailure)
            return fundingResult.Error;
        var fundingAccount = fundingResult.Value;

        // Some layouts list the most recent transaction first; reverse those so the time-ordered
        // BankTransaction.Id minted per row follows BookingDate.
        var ordered = layout.RowsAreMostRecentFirst
            ? statement.Rows.AsEnumerable().Reverse()
            : statement.Rows;

        var bankTransactions = new List<BankTransaction>(statement.Rows.Count);
        foreach (var row in ordered)
        {
            var mapped = ToBankTransaction(bankAccount.Id, row, fundingAccount);
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

        var source = new IngCreditCardStatementSource(file.Content);

        IIngCreditCardStatementParser? layout = null;
        foreach (var candidate in _layouts)
        {
            if (await candidate.CanParseAsync(source, cancellationToken))
            {
                layout = candidate;
                break;
            }
        }

        if (layout is null)
            return null;

        CreditCardStatement statement;
        try
        {
            statement = await layout.ParseStatementAsync(source, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (CsvHelperException)
        {
            return null;
        }
        finally
        {
            if (file.Content.CanSeek)
                file.Content.Seek(0, SeekOrigin.Begin);
        }

        // Anchor on the first row that names a card. Rows that transfer between the card and its
        // Funding account carry no card number (ADR 0038), so Rows[0] is not necessarily one of
        // them. A statement of nothing but such transfers reveals no card at all; fall back to the
        // Overeenkomstnummer in the CSV filename, which resolves against a card whose
        // AccountNumber records it. The filename stays a fallback, never an authority: preferring
        // it would strand cards that have no AccountNumber set.
        var anchor =
            statement
                .Rows.Select(row => Normalize(row.CardNumber))
                .FirstOrDefault(c => c.Length > 0)
            ?? IngAnchor.FromCreditCardFilename(file.FileName);

        return string.IsNullOrEmpty(anchor) ? null : new ImportIdentity(Key, SupportedType, anchor);
    }

    // Only rows that name a card verify the target; a blank card number marks a Funding-account
    // transfer, which names none (ADR 0038). Absence therefore cannot fail, but disagreement must:
    // ADR 0034 makes a content anchor contradicting the chosen account a hard error.
    private static Result EnsureCardMatches(BankAccount bankAccount, CreditCardStatement statement)
    {
        var expected = Normalize(bankAccount.CardIdentifier);
        string? seen = null;

        foreach (var row in statement.Rows)
        {
            var cardNumber = Normalize(row.CardNumber);
            if (cardNumber.Length == 0)
                continue;

            if (cardNumber != expected)
            {
                return new InvariantError(
                    ErrorCodes.ImportIbanMismatch,
                    $"Statement card number '{row.CardNumber}' does not match this "
                        + "Card's CardIdentifier."
                );
            }

            if (seen is not null && cardNumber != seen)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file mixes rows from multiple Accounts; all rows must share "
                        + "the same Account value."
                );
            }

            seen = cardNumber;
        }

        return Result.Success;
    }

    // The configured Funding account wins, and a statement that names a different one is a hard
    // failure (ADR 0038). Scoped by the link's optionality: a card with no Funding account
    // configured still uses whatever the statement names, so the PDF path is unaffected until the
    // link is set.
    private static Result<string?> ResolveFundingAccount(
        BankAccount bankAccount,
        CreditCardStatement statement
    )
    {
        var configured = NullIfBlank(bankAccount.FundingBankAccount?.Iban);
        var stated = NullIfBlank(statement.LinkedAccount);

        if (configured is null)
            return stated;

        if (stated is not null && Normalize(stated) != Normalize(configured))
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement names funding account '{stated}', but this Card is configured to "
                    + $"settle against '{configured}'."
            );
        }

        return configured;
    }

    private Result<BankTransaction> ToBankTransaction(
        BankAccountId bankAccountId,
        CreditCardStatementRow row,
        string? fundingAccountIban
    )
    {
        var description = row.Description;
        var counterpartyName = NullIfBlank(row.Description);
        var signedCents = ToMinorUnits(row.Amount) ?? 0;

        // Only rows that move money between the card and its Funding account get the funding IBAN;
        // merchant rows have the merchant as the counterparty and no counterparty IBAN of their
        // own. Populating it here lets ADR 0012's Attach predicate fire on the card-side pay-down
        // without amending clause (3).
        //
        // Two signals, because the layouts reveal different ones: a blank card number marks such a
        // transfer in the CSV export (nothing but a card/account transfer lacks a card), while PDF
        // statements print a card number on every row and only the transaction type distinguishes
        // them.
        var isFundingTransfer =
            Normalize(row.CardNumber).Length == 0
            || row.TransactionType
                is CreditCardTransactionType.DirectDebit
                    or CreditCardTransactionType.Repayment
                    or CreditCardTransactionType.Correction;
        var counterpartyAccountNumber = isFundingTransfer ? NullIfBlank(fundingAccountIban) : null;

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

        if (ToMinorUnits(row.ForeignCurrencyRate) is { } rate)
            AddInteger(entries, "Foreign Currency Rate", rate);

        if (
            row.ForeignCurrencyMarkUp is { } markUp
            && ToMinorUnits(markUp.Amount) is { } markUpAmount
        )
        {
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
