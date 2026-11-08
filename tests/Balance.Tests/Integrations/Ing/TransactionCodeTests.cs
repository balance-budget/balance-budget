using System.Reflection;
using Balance.Integration.Ing.Models.BankAccount;
using CsvHelper;

namespace Balance.Tests.Integrations.Ing;

internal sealed class TransactionCodeTests
{
    [Test]
    public async Task ToCode_matches_the_name_the_csv_reader_parses()
    {
        foreach (
            var field in typeof(TransactionCode).GetFields(
                BindingFlags.Public | BindingFlags.Static
            )
        )
        {
            var code = (TransactionCode)field.GetValue(null)!;
            var parsed = field.GetCustomAttribute<CsvEnumNameAttribute>()?.Names[0];

            await Assert.That(code.ToCode()).IsEqualTo(parsed);
        }
    }
}
