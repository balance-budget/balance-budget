using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngSavingsAccountStatementParserTests
{
    [Test]
    [Skip("Requires file to be present")]
    public async Task ParsesDutchExport(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "D12345678_01-01-2026_31-01-2026.csv"
        );
        await using var stream = File.OpenRead(path);
        var parser = new IngSavingsAccountStatementParser();
        var result = await parser.ParseStatementsAsync(stream, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(96);
    }

    [Test]
    [Skip("Requires file to be present")]
    public async Task ParsesEnglishExport(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "D12345678_01-03-2026_31-03-2026.csv"
        );
        await using var stream = File.OpenRead(path);
        var parser = new IngSavingsAccountStatementParser();
        var result = await parser.ParseStatementsAsync(stream, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(96);
    }
}
