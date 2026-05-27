namespace Balance.Integration.Ing.Models.CreditCard;

internal sealed class CreditCardStatement
{
    public required string Counterparty { get; init; }
    public required IReadOnlyList<CreditCardStatementRow> Rows { get; init; }
}
