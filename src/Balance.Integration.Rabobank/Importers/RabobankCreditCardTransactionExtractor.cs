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

internal sealed class RabobankCreditCardTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

    // Merchant rows pad the description into fixed columns: merchant, city, country code, then an
    // optional wallet such as "Google Pay". The city is cut off at its width and can run straight
    // into the country code.
    private const int MerchantWidth = 25;
    private const int CityWidth = 13;
    private const int CountryWidth = 3;
    private const int CountryStart = MerchantWidth + CityWidth;
    private const int FixedWidth = CountryStart + CountryWidth;

    private readonly IRabobankCreditCardStatementParser _parser;

    public RabobankCreditCardTransactionExtractor(IRabobankCreditCardStatementParser parser)
    {
        _parser = parser;
    }

    public string Key => "Rabobank.CreditCard";
    public string BankName => "Rabobank";
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
                "Rabobank credit-card statements are imported in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        IReadOnlyList<CreditCardStatementRow>? rows;
        try
        {
            rows = await _parser.ParseStatementsAsync(stream, cancellationToken);
        }
        catch (Exception ex) when (ex is CsvFormatException or KeyNotFoundException)
        {
            return FormatInvalid(ex.Message);
        }

        if (rows is null)
            return FormatInvalid("the file does not carry a Rabobank credit-card header.");

        if (rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var consistency = EnsureSingleCardAndCurrency(bankAccount, rows);
        if (consistency.IsFailure)
            return consistency.Error;

        var fundingResult = ResolveFundingAccount(bankAccount, rows[0].FundingIban);
        if (fundingResult.IsFailure)
            return fundingResult.Error;

        var bankTransactions = new List<BankTransaction>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                bankTransactions.Add(ToBankTransaction(bankAccount.Id, row, fundingResult.Value));
            }
            catch (FormatException ex)
            {
                return FormatInvalid($"row dated {row.Date:yyyy-MM-dd}: {ex.Message}");
            }
        }
        return bankTransactions;
    }

    public async Task<ImportIdentity?> TryIdentifyAsync(
        ImportFile file,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        var fromFilename = RabobankAnchor.FromCreditCardFilename(file.FileName);
        if (fromFilename is not null)
            return new ImportIdentity(Key, SupportedType, fromFilename);

        if (!file.Content.CanSeek)
            return null;

        file.Content.Seek(0, SeekOrigin.Begin);
        IReadOnlyList<CreditCardStatementRow>? rows;
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
            : new ImportIdentity(
                Key,
                SupportedType,
                RabobankAnchor.CardIdentifier(rows[0].CardNumber)
            );
    }

    // The card number is on every row, so a statement for another card fails on its first row
    // (ADR 0034). The Funding account is on every row too and must not vary within one file.
    private static Result EnsureSingleCardAndCurrency(
        BankAccount bankAccount,
        IReadOnlyList<CreditCardStatementRow> rows
    )
    {
        var expected = RabobankAnchor.Normalize(bankAccount.CardIdentifier);
        var funding = RabobankAnchor.Normalize(rows[0].FundingIban);

        foreach (var row in rows)
        {
            var card = RabobankAnchor.CardIdentifier(row.CardNumber);
            if (card != expected)
            {
                return card == RabobankAnchor.CardIdentifier(rows[0].CardNumber)
                    ? new InvariantError(
                        ErrorCodes.ImportIbanMismatch,
                        $"Statement card ending in '{row.CardNumber.Trim()}' does not match this "
                            + "Card's CardIdentifier."
                    )
                    : new InvariantError(
                        ErrorCodes.ImportAccountColumnDivergence,
                        "Statement file mixes rows from multiple cards. Export one card per file."
                    );
            }

            if (RabobankAnchor.Normalize(row.FundingIban) != funding)
            {
                return new InvariantError(
                    ErrorCodes.ImportAccountColumnDivergence,
                    "Statement file names more than one funding account."
                );
            }

            if (!string.Equals(row.Currency.Trim(), Eur.Value, StringComparison.OrdinalIgnoreCase))
            {
                return new InvariantError(
                    ErrorCodes.ImportCurrencyMismatch,
                    $"Rabobank credit-card statements are imported in EUR; row dated "
                        + $"{row.Date:yyyy-MM-dd} is in '{row.Currency}'."
                );
            }
        }

        return Result.Success;
    }

    // The configured Funding account wins, and a statement that names a different one is a hard
    // failure (ADR 0038). A card with no Funding account configured uses the one the statement
    // names.
    private static Result<string?> ResolveFundingAccount(BankAccount bankAccount, string stated)
    {
        var configured = NullIfBlank(bankAccount.FundingBankAccount?.Iban);
        var statedIban = NullIfBlank(stated);

        if (configured is null)
            return statedIban;

        if (
            statedIban is not null
            && RabobankAnchor.Normalize(statedIban) != RabobankAnchor.Normalize(configured)
        )
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement names funding account '{statedIban}', but this Card is configured to "
                    + $"settle against '{configured}'."
            );
        }

        return configured;
    }

    private BankTransaction ToBankTransaction(
        BankAccountId bankAccountId,
        CreditCardStatementRow row,
        string? fundingIban
    )
    {
        var text = row.Description;
        var description = RabobankPatterns.Whitespace().Replace(text, " ").Trim();
        var isFundingTransfer = IsFundingTransfer(description);
        var merchant = isFundingTransfer ? null : SplitMerchant(text);

        // Only rows that move money between the card and its Funding account get the funding IBAN,
        // which lets the Attach predicate pair them with the current-account leg. Merchant rows
        // name the merchant as the counterparty instead.
        string? counterpartyName = null;
        if (!isFundingTransfer)
            counterpartyName = merchant?.Name ?? NullIfBlank(description);

        var foreignAmount = RabobankCsv.ParseOptionalAmount(row.ForeignAmount);

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(RabobankCsv.ToMinorUnits(RabobankCsv.ParseAmount(row.Amount)), Eur),
            Description = description,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = isFundingTransfer ? fundingIban : null,
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ForeignAmount = RabobankCsv.ToMinorUnits(foreignAmount),
            ForeignCurrencyCode = foreignAmount is null ? null : NullIfBlank(row.ForeignCurrency),
            ExchangeRate = RabobankCsv.ParseOptionalAmount(row.ExchangeRate),
            ImporterKey = Key,
            Metadata = new MetadataBuilder()
                .AddString("Rabobank Transaction Reference", row.TransactionReference)
                .AddString("Rabobank Product", row.ProductName)
                .AddString("Card Holder", row.CardHolder)
                .AddString("Funding Account", row.FundingIban)
                .AddString("Card Location", merchant?.City)
                .AddString("Card Country", merchant?.Country)
                .AddString("Wallet", merchant?.Wallet)
                .Build(),
        };
    }

    // The labels Rabobank prints on rows that move money between the card and the current account:
    // the monthly settlement, a manual top-up, and a withdrawal of a positive card balance.
    private static bool IsFundingTransfer(string description) =>
        string.Equals(
            description,
            "Verrekening vorig overzicht",
            StringComparison.OrdinalIgnoreCase
        )
        || description.StartsWith(
            "BIJSCHRIJVING SALDO OP UW CREDITCARD",
            StringComparison.OrdinalIgnoreCase
        )
        || description.Contains(
            "OVERBOEKING VAN CREDITCARD NAAR BETAALREKENING",
            StringComparison.OrdinalIgnoreCase
        );

    private static Merchant? SplitMerchant(string text)
    {
        if (text.Length < FixedWidth)
            return null;

        var country = text.AsSpan(CountryStart, CountryWidth);
        if (
            !char.IsAsciiLetterUpper(country[0])
            || !char.IsAsciiLetterUpper(country[1])
            || !char.IsAsciiLetterUpper(country[2])
        )
            return null;

        if (text.Length > FixedWidth && text[FixedWidth] != ' ')
            return null;

        var name = text[..MerchantWidth].Trim();
        if (name.Length == 0)
            return null;

        return new Merchant(
            name,
            NullIfBlank(text[MerchantWidth..CountryStart]),
            country.ToString(),
            NullIfBlank(text[FixedWidth..])
        );
    }

    private static InvariantError FormatInvalid(string detail) =>
        new(
            ErrorCodes.ImportFormatInvalid,
            $"Failed to parse Rabobank credit-card statement CSV: {detail}"
        );

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Merchant(string Name, string? City, string Country, string? Wallet);
}
