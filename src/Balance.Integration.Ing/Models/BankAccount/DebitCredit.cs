using CsvHelper;

namespace Balance.Integration.Ing.Models.BankAccount;

internal enum DebitCredit
{
    [CsvEnumName("Debit", "Af")]
    Debit,

    [CsvEnumName("Credit", "Bij")]
    Credit,
}
