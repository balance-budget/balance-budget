using Balance.Data.Entities.Enums;
using Balance.Integration.Rabobank.Contracts;

namespace Balance.Integration.Rabobank.Importers;

internal sealed class RabobankCurrentAccountTransactionExtractor
    : RabobankAccountTransactionExtractor
{
    public RabobankCurrentAccountTransactionExtractor(IRabobankAccountStatementParser parser)
        : base(parser) { }

    public override string Key => "Rabobank.CurrentAccount";
    public override BankAccountType SupportedType => BankAccountType.Current;
}
