using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.Statements;
using Balance.Services.Contracts;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngBankTransactionImporter : IBankTransactionImporter
{
    private readonly IIngStatementParser _ingStatementParser;
    private readonly IIngNoteParser _ingNoteParser;

    public IngBankTransactionImporter(
        IIngStatementParser ingStatementParser,
        IIngNoteParser ingNoteParser
    )
    {
        _ingStatementParser = ingStatementParser;
        _ingNoteParser = ingNoteParser;
    }

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        // This method is a stub.
        // Eventually this is where we need to turn the ING-specific statements into balance bank transactions
        const string path = "";
        var statements = await _ingStatementParser.ParseStatementsAsync(path, cancellationToken);
        var reOrderedStatements = statements.Reverse().Select(ToTransaction).ToList();
    }

    private BankTransaction ToTransaction(CurrentAccountStatementRow statement)
    {
        var ingNote = _ingNoteParser.ParseNote(statement.Notifications);
        var isDebit = statement.DebitCredit is DebitCredit.Debit;

        var (sourceAccountNumber, sourceAccountName) = isDebit
            ? (statement.Account, statement.Account)
            : (statement.CounterParty, statement.Description);

        var (destinationAccountNumber, destinationAccountName) = isDebit
            ? (statement.CounterParty, statement.Description)
            : (statement.Account, statement.Account);

        // Fall back to savings account number extracted from the description/note
        if (isDebit && string.IsNullOrEmpty(destinationAccountNumber))
        {
            destinationAccountNumber = GetSavingsAccountNumber(
                statement.Description,
                ingNote.Other
            );
        }
        else if (!isDebit && string.IsNullOrEmpty(sourceAccountNumber))
        {
            sourceAccountNumber = GetSavingsAccountNumber(statement.Description, ingNote.Other);
        }

        sourceAccountNumber ??= string.Empty;
        destinationAccountNumber ??= string.Empty;

        var ibanPrefixPattern = IngPatterns.IbanPrefixPattern();

        return new BankTransaction
        {
            BookingDate = statement.Date,
            BankAccountId = new BankAccountId(Guid.CreateVersion7()),
            Money = new Money((long)(statement.Amount * 100), new CurrencyCode("EUR")),
        };
    }

    private static string? GetSavingsAccountNumber(string description, string? note)
    {
        var pattern = IngPatterns.SavingsAccountPattern();

        var descriptionMatch = pattern.Match(description);
        if (descriptionMatch.Success)
            return descriptionMatch.Value;

        if (note is not null)
        {
            var noteMatch = pattern.Match(note);
            if (noteMatch.Success)
                return noteMatch.Value;
        }

        return null;
    }
}
