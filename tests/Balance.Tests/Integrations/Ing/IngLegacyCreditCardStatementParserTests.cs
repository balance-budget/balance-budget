using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngLegacyCreditCardStatementParserTests
{
    [Test]
    [Skip("Requires file to be present")]
    public async Task ParsesStatement(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "creditcard-legacy.pdf"
        );

        await using var stream = File.OpenRead(path);
        var lines = IngCreditCardPdfReader.ExtractLines(stream, cancellationToken);
        var parser = new IngLegacyCreditCardStatementParser();
        await Assert.That(parser.CanParse(lines)).IsTrue();
        var result = parser.ParseStatement(lines, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Rows.Count).IsGreaterThan(0);

        var first = result.Rows[0];
        await Assert.That(first.Description).IsNotEmpty();
        await Assert.That(first.RawRecord).IsNotEmpty();
    }
}
