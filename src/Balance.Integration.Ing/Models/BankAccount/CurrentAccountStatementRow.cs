using CsvHelper;

namespace Balance.Integration.Ing.Models.BankAccount;

[CsvRecord]
internal sealed partial class CurrentAccountStatementRow
{
    [CsvColumn("Date", "Datum")]
    [CsvFormat("yyyyMMdd")]
    public required DateOnly Date { get; init; }

    [CsvColumn("Name / Description", "Naam / Omschrijving")]
    public required string Description { get; init; } = string.Empty;

    [CsvColumn("Account", "Rekening")]
    public required string Account { get; init; } = string.Empty;

    [CsvColumn("Counterparty", "Tegenrekening")]
    public required string CounterParty { get; init; } = string.Empty;

    [CsvColumn("Code")]
    public required TransactionCode Code { get; init; }

    [CsvColumn("Debit/credit", "Af Bij")]
    public required DebitCredit DebitCredit { get; init; }

    [CsvColumn("Amount (EUR)", "Bedrag (EUR)")]
    public required decimal Amount { get; init; }

    [CsvColumn("Transaction type", "Mutatiesoort")]
    public required string TransactionType { get; init; } = string.Empty;

    [CsvColumn("Notifications", "Mededelingen")]
    public required string Notifications { get; init; } = string.Empty;

    [CsvColumn("Resulting balance", "Saldo na mutatie")]
    public required decimal ResultingBalance { get; init; }

    [CsvColumn("Tag")]
    public required string Tag { get; init; } = string.Empty;

    // Filled in by the parser from the reader's current record, not by a column.
    [CsvIgnore]
    public string RawRecord { get; set; } = string.Empty;
}
