using Balance.Integration.Ing.Models.CreditCard;
using Balance.Integration.Ing.Parsers;

namespace Balance.Integration.Ing.Importers;

internal sealed class IngModernCreditCardTransactionExtractor : IngCreditCardTransactionExtractor
{
    public IngModernCreditCardTransactionExtractor(IngModernCreditCardStatementParser parser)
        : base(parser) { }

    public override string Key => "Ing.CreditCard.V2";

    protected override IEnumerable<CreditCardStatementRow> OrderForInsertion(
        IReadOnlyList<CreditCardStatementRow> rows
    ) => rows.Reverse();
}
