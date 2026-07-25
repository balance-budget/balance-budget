using Balance.Integration.Pdf;
using Balance.Integration.Stater.Parsers;

namespace Balance.Tests.Integrations.Stater;

internal sealed class StaterConstructionDepositStatementParserTests
{
    [Test]
    [Skip("Requires file to be present")]
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
            "construction-deposit.pdf"
        );

        await using var stream = File.OpenRead(path);
        var lines = PdfTextReader.ExtractLines(stream, cancellationToken);
        var parser = new StaterStatementParser();
        var result = parser.Parse(lines);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Rows.Count).IsEqualTo(3);

        var first = result.Rows[0];
        await Assert.That(first.Description).IsNotEmpty();
        await Assert.That(first.RawRecord).IsNotEmpty();
    }
}
