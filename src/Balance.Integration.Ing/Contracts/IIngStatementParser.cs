using Balance.Integration.Ing.Models.Statements;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngStatementParser
{
    public ValueTask<IReadOnlyList<IngStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
