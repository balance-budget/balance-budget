using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngModernCreditCardStatementParserTests
{
    [Test]
    [Skip("Requires file to be present")]
    public async Task ParsesStatement(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "creditcard-modern.pdf"
        );

        await using var stream = File.OpenRead(path);
        var lines = IngCreditCardPdfReader.ExtractLines(stream, cancellationToken);
        var parser = new IngModernCreditCardStatementParser();
        await Assert.That(parser.CanParse(lines)).IsTrue();
        var result = parser.ParseStatement(lines, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Rows.Count).IsGreaterThan(0);

        var first = result.Rows[0];
        await Assert.That(first.Description).IsNotEmpty();
        await Assert.That(first.RawRecord).IsNotEmpty();
    }
}
