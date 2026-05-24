using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Statements;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngBankTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

    private readonly IIngStatementParser _ingStatementParser;

    public IngBankTransactionExtractor(IIngStatementParser ingStatementParser)
    {
        _ingStatementParser = ingStatementParser;
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

        var bankTransactions = new List<BankTransaction>(rows.Count);
        foreach (var row in rows)
        {
            bankTransactions.Add(ToBankTransaction(bankAccount.Id, row));
        }
        return bankTransactions;
    }

    private static BankTransaction ToBankTransaction(
        BankAccountId bankAccountId,
        IngStatementRow row
    )
    {
        var signedCents = ToSignedCents(row.Parsed.Amount, row.Parsed.DebitCredit);
        var counterpartyAccountNumber = string.IsNullOrWhiteSpace(row.Parsed.CounterParty)
            ? null
            : row.Parsed.CounterParty;
        var counterpartyName = string.IsNullOrWhiteSpace(row.Parsed.Description)
            ? null
            : row.Parsed.Description;
        var rawSource = RowHasher.Normalise(row.RawRecord);

        return new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = row.Parsed.Date,
            Money = new Money(signedCents, Eur),
            Description = row.Parsed.Notifications,
            CounterpartyName = counterpartyName,
            CounterpartyAccountNumber = counterpartyAccountNumber,
            RawSource = rawSource,
            RowHash = RowHasher.Hash(row.RawRecord),
        };
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
}
