namespace Balance.Integration.Ing.Models.CreditCard;

internal sealed class CreditCardStatement
{
    public required string LinkedAccount { get; init; }
    public required IReadOnlyList<CreditCardStatementRow> Rows { get; init; }
}
