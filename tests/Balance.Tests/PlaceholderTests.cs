namespace Balance.Tests;

internal sealed class PlaceholderTests
{
    [Test]
    public async Task Test()
    {
        var value = true;
        await Assert.That(value).IsTrue();
    }
}
