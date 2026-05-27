namespace Balance.Integration.Ing.Models.BankAccount;

internal sealed record IngStatementRow(CurrentAccountStatementRow Parsed, string RawRecord);
