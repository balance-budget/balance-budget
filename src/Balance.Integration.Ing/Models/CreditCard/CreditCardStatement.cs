namespace Balance.Integration.Ing.Models.CreditCard;

internal sealed class CreditCardStatement
{
    /// <summary>
    /// The funding account the statement itself names, normalized. Null when the layout does not
    /// carry one (the CSV export does not), in which case the extractor falls back to the Card
    /// BankAccount's configured Funding account (ADR 0038).
    /// </summary>
    public required string? LinkedAccount { get; init; }
    public required IReadOnlyList<CreditCardStatementRow> Rows { get; init; }
}
