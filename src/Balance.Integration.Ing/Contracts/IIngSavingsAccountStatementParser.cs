using Balance.Integration.Ing.Models.BankAccount;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngSavingsAccountStatementParser
{
    public ValueTask<IReadOnlyList<SavingsAccountStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
