namespace Balance.Integration.Ing.Models.Statements;

internal sealed record IngStatementRow(CurrentAccountStatementRow Parsed, string RawRecord);
