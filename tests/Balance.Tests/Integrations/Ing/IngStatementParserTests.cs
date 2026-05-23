using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngStatementParserTests
{
    [Test]
    public async Task ParsesDutchExport(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "NL69INGB0123456789_01-01-2026_31-01-2026.csv"
        );
        var parser = new IngStatementParser();
        var result = await parser.ParseStatementsAsync(path, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(96);
    }

    [Test]
    public async Task ParsesEnglishExport(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "NL69INGB0123456789_01-03-2026_31-03-2026.csv"
        );
        var parser = new IngStatementParser();
        var result = await parser.ParseStatementsAsync(path, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(96);
    }
}
