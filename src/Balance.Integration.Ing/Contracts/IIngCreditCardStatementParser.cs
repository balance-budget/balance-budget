using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngCreditCardStatementParser
{
    public ValueTask<CreditCardStatement> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
