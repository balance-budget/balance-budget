using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.Statements;

public enum DebitCredit
{
    [Name("Debit", "Af")]
    Debit,

    [Name("Credit", "Bij")]
    Credit,
}
