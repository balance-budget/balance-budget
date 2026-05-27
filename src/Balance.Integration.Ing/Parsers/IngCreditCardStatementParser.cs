using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.Statements;

namespace Balance.Integration.Ing.Parsers;

internal sealed class IngCreditCardStatementParser : IIngCreditCardStatementParser
{
    public async ValueTask<IReadOnlyList<IngStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(stream);
        return [];
    }
}
