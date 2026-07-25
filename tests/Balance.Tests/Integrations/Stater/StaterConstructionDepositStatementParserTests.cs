using Balance.Integration.Stater.Helpers;
using Balance.Integration.Stater.Parsers;

namespace Balance.Tests.Integrations.Stater;

internal sealed class StaterConstructionDepositStatementParserTests
{
    [Test]
    [Skip("Requires a real Stater statement PDF fixture")]
    public async Task ParsesStatement(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Stater",
            "construction-deposit.pdf"
        );
        await using var stream = File.OpenRead(path);
        var lines = StaterPdfReader.ExtractLines(stream, cancellationToken);
        var parser = new StaterStatementParser();
        var result = parser.Parse(lines);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Rows.Count).IsEqualTo(3);

        var first = result.Rows[0];
        await Assert.That(first.Description).IsNotEmpty();
        await Assert.That(first.RawRecord).IsNotEmpty();
    }
}
