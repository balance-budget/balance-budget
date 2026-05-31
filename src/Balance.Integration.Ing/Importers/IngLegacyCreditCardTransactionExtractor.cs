using Balance.Integration.Ing.Parsers;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngLegacyCreditCardTransactionExtractor : IngCreditCardTransactionExtractor
{
    public IngLegacyCreditCardTransactionExtractor(IngLegacyCreditCardStatementParser parser)
        : base(parser) { }

    public override string Key => "Ing.CreditCard.V1";
}
