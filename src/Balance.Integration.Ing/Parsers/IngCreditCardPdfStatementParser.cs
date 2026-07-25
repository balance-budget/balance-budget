using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Parsers;

// Shared scaffolding for the PDF credit-card statement layouts. The statement source turns the PDF
// into text lines once per file; the per-layout subclasses only differ in how they recognize
// (CanParse) and interpret those lines into a CreditCardStatement. A non-PDF drop yields no lines,
// which is how these layouts decline a CSV without the PDF reader ever seeing it.
internal abstract class IngCreditCardPdfStatementParser : IIngCreditCardStatementParser
{
    protected static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    public abstract bool CanParse(IReadOnlyList<string> lines);

    public virtual bool RowsAreMostRecentFirst => false;

    public abstract CreditCardStatement ParseStatement(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken
    );

    public ValueTask<bool> CanParseAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return ValueTask.FromResult(
            source.LooksLikePdf && CanParse(source.GetLines(cancellationToken))
        );
    }

    public ValueTask<CreditCardStatement> ParseStatementAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return ValueTask.FromResult(
            ParseStatement(source.GetLines(cancellationToken), cancellationToken)
        );
    }

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
