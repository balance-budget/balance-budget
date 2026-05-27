namespace Balance.Integration.Ing.Models.CreditCard;

/// <remarks>
/// Names are taken from the Dutch labels used in ING credit-card PDF statements.
/// </remarks>
internal enum CreditCardTransactionType
{
    /// <summary>
    /// Betaling
    /// </summary>
    Payment,

    /// <summary>
    /// Ontvangst
    /// </summary>
    Receipt,

    /// <summary>
    /// Incasso
    /// </summary>
    DirectDebit,

    /// <summary>
    /// Geldopname
    /// </summary>
    CashWithdrawal,

    /// <summary>
    /// Kosten
    /// </summary>
    Fees,

    /// <summary>
    /// Correctie
    /// </summary>
    Correction,
}
