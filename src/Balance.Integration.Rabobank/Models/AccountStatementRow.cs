using CsvHelper;

namespace Balance.Integration.Rabobank.Models;

// One row of a Rabobank current- or savings-account CSV export; both account types share this
// layout. Amounts are read as text because they carry an explicit sign and a Dutch decimal comma,
// and the foreign-amount columns are usually blank.
[CsvRecord]
internal sealed partial class AccountStatementRow
{
    [CsvColumn("IBAN/BBAN")]
    public required string Iban { get; init; } = string.Empty;

    [CsvColumn("Munt")]
    public required string Currency { get; init; } = string.Empty;

    [CsvColumn("Volgnr")]
    public required string SequenceNumber { get; init; } = string.Empty;

    [CsvColumn("Datum")]
    [CsvFormat("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [CsvColumn("Rentedatum")]
    [CsvFormat("yyyy-MM-dd")]
    public required DateOnly ValueDate { get; init; }

    [CsvColumn("Bedrag")]
    public required string Amount { get; init; } = string.Empty;

    [CsvColumn("Tegenrekening IBAN/BBAN")]
    public required string CounterpartyIban { get; init; } = string.Empty;

    [CsvColumn("Naam tegenpartij")]
    public required string CounterpartyName { get; init; } = string.Empty;

    [CsvColumn("Naam uiteindelijke partij")]
    public required string UltimateParty { get; init; } = string.Empty;

    [CsvColumn("Naam initiërende partij")]
    public required string InitiatingParty { get; init; } = string.Empty;

    [CsvColumn("BIC tegenpartij")]
    public required string CounterpartyBic { get; init; } = string.Empty;

    [CsvColumn("Code")]
    public required string Code { get; init; } = string.Empty;

    [CsvColumn("Batch ID")]
    public required string BatchId { get; init; } = string.Empty;

    [CsvColumn("Transactiereferentie")]
    public required string TransactionReference { get; init; } = string.Empty;

    [CsvColumn("Machtigingskenmerk")]
    public required string MandateId { get; init; } = string.Empty;

    [CsvColumn("Incassant ID")]
    public required string CreditorId { get; init; } = string.Empty;

    [CsvColumn("Betalingskenmerk")]
    public required string PaymentReference { get; init; } = string.Empty;

    [CsvColumn("Omschrijving-1")]
    public required string Description1 { get; init; } = string.Empty;

    [CsvColumn("Omschrijving-2")]
    public required string Description2 { get; init; } = string.Empty;

    [CsvColumn("Omschrijving-3")]
    public required string Description3 { get; init; } = string.Empty;

    [CsvColumn("Reden retour")]
    public required string ReturnReason { get; init; } = string.Empty;

    [CsvColumn("Oorspr bedrag")]
    public required string ForeignAmount { get; init; } = string.Empty;

    [CsvColumn("Oorspr munt")]
    public required string ForeignCurrency { get; init; } = string.Empty;

    [CsvColumn("Koers")]
    public required string ExchangeRate { get; init; } = string.Empty;

    // Filled in by the parser from the reader's current record, not by a column.
    [CsvIgnore]
    public string RawRecord { get; set; } = string.Empty;
}
