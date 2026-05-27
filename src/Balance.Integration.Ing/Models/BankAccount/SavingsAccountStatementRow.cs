using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.BankAccount;

public sealed class SavingsAccountStatementRow
{
    [Name("Date")]
    [Format("yyyy-MM-dd")]
    public required DateOnly Date { get; init; }

    [Name("Description")]
    public required string Description { get; init; } = string.Empty;

    [Name("Account")]
    public required string Account { get; init; } = string.Empty;

    [Name("Account name")]
    public required string AccountName { get; init; } = string.Empty;

    [Name("Counterparty")]
    public required string CounterParty { get; init; } = string.Empty;

    [Name("Debit/credit")]
    public required DebitCredit DebitCredit { get; init; }

    [Name("Amount")]
    public required decimal Amount { get; init; }

    [Name("Currency")]
    public required string Currency { get; init; } = string.Empty;

    [Name("Transaction type")]
    public required string TransactionType { get; init; } = string.Empty;

    [Name("Notifications")]
    public required string Notifications { get; init; } = string.Empty;

    [Name("Resulting balance")]
    public required decimal ResultingBalance { get; init; }

    public required string RawRecord { get; set; } = string.Empty;
}
