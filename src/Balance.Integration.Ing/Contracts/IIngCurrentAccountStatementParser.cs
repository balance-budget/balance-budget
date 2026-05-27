using Balance.Integration.Ing.Models.BankAccount;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngCurrentAccountStatementParser
{
    public ValueTask<IReadOnlyList<CurrentAccountStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
