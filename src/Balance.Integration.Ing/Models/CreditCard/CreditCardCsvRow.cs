using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper;

namespace Balance.Integration.Ing.Models.CreditCard;

// One row of an ING credit-card CSV export, in either dialect. The Date column is ISO in both; the
// decimal culture is applied at read time from the detected dialect, not from these attributes.
[CsvRecord]
internal sealed partial class CreditCardCsvRow
{
    [CsvColumn("Date", "Datum")]
    [CsvFormat("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [CsvColumn("Name / Description", "Naam / Omschrijving")]
    public required string Description { get; init; } = string.Empty;

    [CsvColumn("Transaction type", "Mutatiesoort")]
    public required string TransactionType { get; init; } = string.Empty;

    [CsvColumn("Debit/credit", "Af Bij")]
    public required DebitCredit DebitCredit { get; init; }

    [CsvColumn("Amount (EUR)", "Bedrag (EUR)")]
    public required decimal Amount { get; init; }

    [CsvColumn("Notifications", "Mededelingen")]
    public required string Notifications { get; init; } = string.Empty;

    /// <summary>
    /// Blank on rows that move money between the card and its Funding account — no card was
    /// involved, which is exactly what marks them (ADR 0038).
    /// </summary>
    [CsvColumn("Card number", "Kaartnummer")]
    public required string CardNumber { get; init; } = string.Empty;
}
