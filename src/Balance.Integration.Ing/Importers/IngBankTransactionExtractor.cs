using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.Notes;
using Balance.Integration.Ing.Models.Statements;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using CsvHelper;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngBankTransactionExtractor : IBankTransactionExtractor
{
    private static readonly CurrencyCode Eur = new("EUR");

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
        };
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
