using Balance.Integration.Ing.Models.Statements;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngStatementParser
{
    public ValueTask<IReadOnlyList<CurrentAccountStatementRow>> ParseStatementsAsync(
        string path,
        CancellationToken cancellationToken
    );
}
