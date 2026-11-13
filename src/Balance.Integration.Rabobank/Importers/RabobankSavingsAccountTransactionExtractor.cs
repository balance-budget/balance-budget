using Balance.Data.Entities.Enums;
using Balance.Integration.Rabobank.Contracts;

namespace Balance.Integration.Rabobank.Importers;

internal sealed class RabobankSavingsAccountTransactionExtractor
    : RabobankAccountTransactionExtractor
{
    public RabobankSavingsAccountTransactionExtractor(IRabobankAccountStatementParser parser)
        : base(parser) { }

    public override string Key => "Rabobank.SavingsAccount";
    public override BankAccountType SupportedType => BankAccountType.Savings;
}
