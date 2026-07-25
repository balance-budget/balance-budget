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
    /// Aflossing
    /// </summary>
    Repayment,

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

    /// <summary>
    /// Diversen — the CSV export's bucket for card/funding-account transfers.
    /// </summary>
    Miscellaneous,

    /// <summary>
    /// A label no layout recognizes. ING adds labels over time, so an unknown one degrades to this
    /// rather than failing the import; the raw row survives in RawSource either way.
    /// </summary>
    Unknown,
}
