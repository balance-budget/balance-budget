using System.Globalization;
using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Models.CreditCard;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Parsers;

// Shared scaffolding for the ING credit-card statement parsers. Both layouts arrive as PDFs
// whose visual lines have to be reconstructed from positioned words; the per-layout subclasses
// only differ in how they interpret those lines into a CreditCardStatement.
internal abstract class IngCreditCardStatementParser : IIngCreditCardStatementParser
{
    private const double LineYTolerance = 0.5;

    protected static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    public ValueTask<CreditCardStatement> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var lines = ExtractLines(stream, cancellationToken);
        return ValueTask.FromResult(ParseStatement(lines, cancellationToken));
    }

    protected abstract CreditCardStatement ParseStatement(
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

    private static List<string> ExtractLines(Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.AddRange(ExtractPageLines(page));
        }

        return lines;
    }

    // Group words by Y (with tolerance) to reconstruct visual lines. PDF coordinates put
    // the origin at the bottom-left, so larger Y is higher on the page — order descending
    // for top-down, then ascending X within each line for left-to-right.
    private static IEnumerable<string> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
}
