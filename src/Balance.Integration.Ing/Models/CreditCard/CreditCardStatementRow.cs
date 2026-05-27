namespace Balance.Integration.Ing.Models.CreditCard;

internal sealed class CreditCardStatementRow
{
    public required DateOnly Date { get; init; }

    public required string Description { get; init; }

    public required CreditCardTransactionType TransactionType { get; init; }

    /// <summary>
    /// Signed amount in EUR. Positive for credits (Ontvangst/Correctie+), negative for debits.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Whitelisted note lines that immediately follow the transaction line, joined by newline.
    /// Empty when no notes were found.
    /// </summary>
    public required string Notes { get; init; }

    public required string RawRecord { get; init; }
}
