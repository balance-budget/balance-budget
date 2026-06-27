using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Parsers;

// Shared scaffolding for the ING credit-card statement layouts. The PDF lines are extracted once
// by the extractor (IngCreditCardPdfReader); the per-layout subclasses only differ in how they
// recognize (CanParse) and interpret those lines into a CreditCardStatement.
internal abstract class IngCreditCardStatementParser : IIngCreditCardStatementParser
{
    protected static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    public abstract bool CanParse(IReadOnlyList<string> lines);

    public virtual bool RowsAreMostRecentFirst => false;

    public abstract CreditCardStatement ParseStatement(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken
    );

    protected static string NormalizeLinkedAccount(string value) =>
        value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    protected static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture);

    // The transaction-line amount includes the leading +/- and may carry whitespace
    // between sign and digits (e.g. "+ 12,34"). Collapse the space so decimal.Parse with
    // nl-NL handles the sign and comma decimal mark in one shot.
    protected static decimal ParseAmount(string captured) =>
        decimal.Parse(
            captured.Replace(" ", string.Empty, StringComparison.Ordinal),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            NlCulture
        );
}
