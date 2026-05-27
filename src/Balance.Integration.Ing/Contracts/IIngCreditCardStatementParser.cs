using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngCreditCardStatementParser
{
    public ValueTask<IReadOnlyList<CreditCardStatementRow>> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
