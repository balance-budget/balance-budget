using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Models.CreditCard;

// The parsed 'Mededelingen' / 'Notifications' column of a credit-card CSV row. A different
// vocabulary from the current-account note (IngNote), which is why it is its own type parsed by its
// own dialect configuration (ADR 0038).
internal sealed class CreditCardCsvNote
{
    public DateOnly? TransactionDate { get; set; }

    /// <summary>
    /// The note's copy of the card number. The row has a dedicated column for it, which is what
    /// verification keys on; this is only cross-checked.
    /// </summary>
    public string? CardNumber { get; set; }

    public CurrencyAmount? ForeignCurrencyAmount { get; set; }
    public decimal? ForeignCurrencyRate { get; set; }

    /// <summary>
    /// The exchange mark-up. Dutch calls it <c>Koersopslag</c> and English calls it <c>Fee</c>, for
    /// the same field with the same value; the Dutch label names it correctly, and it maps to the
    /// mark-up the PDF layouts already report rather than to a separate fee.
    /// </summary>
    public CurrencyAmount? ForeignCurrencyMarkUp { get; set; }

    public string? Other { get; set; }
}
