using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.BankAccount;

internal enum DebitCredit
{
    [Name("Debit", "Af")]
    Debit,

    [Name("Credit", "Bij")]
    Credit,
}
