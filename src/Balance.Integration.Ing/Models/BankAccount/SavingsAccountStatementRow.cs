using CsvHelper;

namespace Balance.Integration.Ing.Models.BankAccount;

[CsvRecord]
internal sealed partial class SavingsAccountStatementRow
{
    [CsvColumn("Date")]
    [CsvFormat("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [CsvColumn("Description")]
    public required string Description { get; init; } = string.Empty;

    [CsvColumn("Account")]
    public required string Account { get; init; } = string.Empty;

    [CsvColumn("Account name")]
    public required string AccountName { get; init; } = string.Empty;

    [CsvColumn("Counterparty")]
    public required string CounterParty { get; init; } = string.Empty;

    [CsvColumn("Debit/credit")]
    public required DebitCredit DebitCredit { get; init; }

    [CsvColumn("Amount")]
    public required decimal Amount { get; init; }

    [CsvColumn("Currency")]
    public required string Currency { get; init; } = string.Empty;

    [CsvColumn("Transaction type")]
    public required string TransactionType { get; init; } = string.Empty;

    [CsvColumn("Notifications")]
    public required string Notifications { get; init; } = string.Empty;

    [CsvColumn("Resulting balance")]
    public required decimal ResultingBalance { get; init; }

    // Filled in by the parser from the reader's current record, not by a column.
    [CsvIgnore]
    public string RawRecord { get; set; } = string.Empty;
}
