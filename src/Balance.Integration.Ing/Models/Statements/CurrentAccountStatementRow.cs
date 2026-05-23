using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.Statements;

public sealed class CurrentAccountStatementRow
{
    [Name("Date", "Datum")]
    [Format("yyyyMMdd")]
    public required DateOnly Date { get; init; }

    [Name("Name / Description", "Naam / Omschrijving")]
    public required string Description { get; init; } = string.Empty;

    [Name("Account", "Rekening")]
    public required string Account { get; init; } = string.Empty;

    [Name("Counterparty", "Tegenrekening")]
    public required string CounterParty { get; init; } = string.Empty;

    [Name("Code")]
    public required TransactionCode Code { get; init; }

    [Name("Debit/credit", "Af Bij")]
    public required DebitCredit DebitCredit { get; init; }

    [Name("Amount (EUR)", "Bedrag (EUR)")]
    public required decimal Amount { get; init; }

    [Name("Transaction type", "Mutatiesoort")]
    public required string TransactionType { get; init; } = string.Empty;

    [Name("Notifications", "Mededelingen")]
    public required string Notifications { get; init; } = string.Empty;

    [Name("Resulting balance", "Saldo na mutatie")]
    public required decimal ResultingBalance { get; init; }

    [Name("Tag")]
    public required string Tag { get; init; } = string.Empty;
}
