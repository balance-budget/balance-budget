using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngCurrentAccountStatementParserTests
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
        await using var stream = File.OpenRead(path);
        var parser = new IngCurrentAccountStatementParser();
        var result = await parser.ParseStatementsAsync(stream, cancellationToken);

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
        await using var stream = File.OpenRead(path);
        var parser = new IngCurrentAccountStatementParser();
        var result = await parser.ParseStatementsAsync(stream, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(96);
    }

    [Test]
    public async Task Exposes_raw_record_per_row(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Ing",
            "NL69INGB0123456789_01-01-2026_31-01-2026.csv"
        );
        await using var stream = File.OpenRead(path);
        var parser = new IngCurrentAccountStatementParser();
        var result = await parser.ParseStatementsAsync(stream, cancellationToken);

        var first = result[0];
        await Assert.That(first.RawRecord).IsNotEmpty();
        await Assert.That(first.RawRecord).Contains(first.Account);
        await Assert.That(first.RawRecord).Contains(first.Description);
    }
}
