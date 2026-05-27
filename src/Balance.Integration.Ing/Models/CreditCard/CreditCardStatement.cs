namespace Balance.Integration.Ing.Models.CreditCard;

internal sealed class CreditCardStatement
{
    public required string Account { get; init; }
    public required IReadOnlyList<CreditCardStatementRow> Rows { get; init; }
}
