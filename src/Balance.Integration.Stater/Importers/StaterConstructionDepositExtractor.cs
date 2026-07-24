using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Stater.Contracts;
using Balance.Integration.Stater.Helpers;
using Balance.Integration.Stater.Models;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;

namespace Balance.Integration.Stater.Importers;

// Savings is the only BankAccountType whose CHECK permits a bare AccountNumber (no IBAN); the
// bouwdepot's header account number equals the loan number, not an IBAN.
internal sealed class StaterConstructionDepositExtractor : IBankTransactionExtractor
{
    private const string ImporterKey = "Stater.ConstructionDeposit";
    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IStaterStatementParser _parser;

    public StaterConstructionDepositExtractor(IStaterStatementParser parser)
    {
        _parser = parser;
    }

    public string Key => ImporterKey;

    // The servicing platform that owns the layout, not the consumer lender; the account's own
    // BankAccount.BankName carries the actual lender.
    public string BankName => "Stater";

    public BankAccountType SupportedType => BankAccountType.Savings;

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
                "Stater construction-deposit statements are in EUR; this BankAccount uses "
                    + $"{bankAccount.CurrencyCode?.Value ?? "(none)"}."
            );
        }

        List<string> lines;
        try
        {
            lines = StaterPdfReader.ExtractLines(stream, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                $"Failed to read Stater statement PDF: {ex.Message}"
            );
        }

        var statement = _parser.Parse(lines);
        if (statement is null)
        {
            return new InvariantError(
                ErrorCodes.ImportFormatInvalid,
                "This file does not match a known Stater construction-deposit statement layout."
            );
        }

        var ownIdentifiers = OwnIdentifiers(bankAccount);
        if (!ownIdentifiers.Contains(Normalize(statement.AccountNumber)))
        {
            return new InvariantError(
                ErrorCodes.ImportIbanMismatch,
                $"Statement account number '{statement.AccountNumber}' does not match this "
                    + "BankAccount's AccountNumber or Iban."
            );
        }

        if (statement.Rows.Count == 0)
            return Array.Empty<BankTransaction>();

        var bankTransactions = new List<BankTransaction>(statement.Rows.Count);
        foreach (var row in statement.Rows)
            bankTransactions.Add(ToBankTransaction(bankAccount.Id, row));
        return bankTransactions;
    }

    public Task<ImportIdentity?> TryIdentifyAsync(
        ImportFile file,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!StaterPdfReader.LooksLikePdf(file.Content))
            return Task.FromResult<ImportIdentity?>(null);

        List<string> lines;
        try
        {
            lines = StaterPdfReader.ExtractLines(file.Content, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult<ImportIdentity?>(null);
        }
        finally
        {
            if (file.Content.CanSeek)
                file.Content.Seek(0, SeekOrigin.Begin);
        }

        var statement = _parser.Parse(lines);
        var identity = statement is null
            ? null
            : new ImportIdentity(ImporterKey, SupportedType, Normalize(statement.AccountNumber));
        return Task.FromResult(identity);
    }

    private static BankTransaction ToBankTransaction(
        BankAccountId bankAccountId,
        StaterStatementRow row
    ) =>
        new()
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Date,
            Money = new Money(row.AmountMinorUnits, Eur),
            Description = row.Description,
            CounterpartyName = row.CounterpartyName,
            CounterpartyAccountNumber = null,
            RawSource = RowHasher.Normalize(row.RawRecord),
            RowHash = RowHasher.Hash(row.RawRecord),
            ImporterKey = ImporterKey,
        };

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
}
