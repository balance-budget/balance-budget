using CsvHelper;

namespace Balance.Integration.Rabobank.Models;

// One row of a Rabobank credit-card CSV export. Every row names the card's Funding account in
// 'Tegenrekening IBAN', so that column identifies the settling account, not a counterparty.
[CsvRecord]
internal sealed partial class CreditCardStatementRow
{
    [CsvColumn("Tegenrekening IBAN")]
    public required string FundingIban { get; init; } = string.Empty;

    [CsvColumn("Munt")]
    public required string Currency { get; init; } = string.Empty;

    // Only the last four digits of the card number.
    [CsvColumn("Creditcard Nummer")]
    public required string CardNumber { get; init; } = string.Empty;

    [CsvColumn("Productnaam")]
    public required string ProductName { get; init; } = string.Empty;

    [CsvColumn("Creditcard Regel1")]
    public required string CardHolder { get; init; } = string.Empty;

    [CsvColumn("Transactiereferentie")]
    public required string TransactionReference { get; init; } = string.Empty;

    [CsvColumn("Datum")]
    [CsvFormat("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [CsvColumn("Bedrag")]
    public required string Amount { get; init; } = string.Empty;

    [CsvColumn("Omschrijving")]
    public required string Description { get; init; } = string.Empty;

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
