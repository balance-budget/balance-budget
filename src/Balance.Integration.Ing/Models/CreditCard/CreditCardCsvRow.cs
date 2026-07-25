using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.CreditCard;

// One row of an ING credit-card CSV export, in either dialect. The Date column is ISO in both; the
// decimal culture is applied at read time from the detected dialect, not from these attributes.
internal sealed class CreditCardCsvRow
{
    [Name("Date", "Datum")]
    [Format("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [Name("Name / Description", "Naam / Omschrijving")]
    public required string Description { get; init; } = string.Empty;

    [Name("Transaction type", "Mutatiesoort")]
    public required string TransactionType { get; init; } = string.Empty;

    [Name("Debit/credit", "Af Bij")]
    public required DebitCredit DebitCredit { get; init; }

    [Name("Amount (EUR)", "Bedrag (EUR)")]
    public required decimal Amount { get; init; }

    [Name("Notifications", "Mededelingen")]
    public required string Notifications { get; init; } = string.Empty;

    /// <summary>
    /// Blank on rows that move money between the card and its Funding account — no card was
    /// involved, which is exactly what marks them (ADR 0038).
    /// </summary>
    [Name("Card number", "Kaartnummer")]
    public required string CardNumber { get; init; } = string.Empty;
}
