using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngModernCreditCardStatementParserTests
{
    [Test]
    [Explicit]
    public async Task ParsesStatement(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "tests",
            "data",
            "cc-modern.pdf"
        );

        await using var stream = File.OpenRead(path);
        var source = new IngCreditCardStatementSource(stream);
        var parser = new IngModernCreditCardStatementParser();
        await Assert.That(await parser.CanParseAsync(source, cancellationToken)).IsTrue();
        var result = await parser.ParseStatementAsync(source, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Rows.Count).IsGreaterThan(0);

        var first = result.Rows[0];
        await Assert.That(first.Description).IsNotEmpty();
        await Assert.That(first.RawRecord).IsNotEmpty();
    }
}
