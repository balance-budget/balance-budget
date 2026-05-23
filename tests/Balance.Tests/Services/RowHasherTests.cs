using Balance.Services.BankTransactions;

namespace Balance.Tests.Services;

internal sealed class RowHasherTests
{
    [Test]
    public async Task Hash_is_deterministic()
    {
        var a = RowHasher.Hash("hello world");
        var b = RowHasher.Hash("hello world");

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Hash_is_64_lowercase_hex_chars()
    {
        var hash = RowHasher.Hash("some statement row");

        await Assert.That(hash.Length).IsEqualTo(64);
        await Assert.That(hash).Matches("^[0-9a-f]{64}$");
    }

    [Test]
    public async Task Hash_treats_crlf_and_lf_as_equivalent()
    {
        var crlf = RowHasher.Hash("a\r\nb\r\nc");
        var lf = RowHasher.Hash("a\nb\nc");

        await Assert.That(crlf).IsEqualTo(lf);
    }

    [Test]
    public async Task Hash_ignores_trailing_whitespace_per_line()
    {
        var trimmed = RowHasher.Hash("a\nb\nc");
        var withTrailing = RowHasher.Hash("a   \nb\t\t\nc \t");

        await Assert.That(trimmed).IsEqualTo(withTrailing);
    }

    [Test]
    public async Task Hash_preserves_leading_whitespace()
    {
        var noLeading = RowHasher.Hash("a");
        var withLeading = RowHasher.Hash("  a");

        await Assert.That(noLeading).IsNotEqualTo(withLeading);
    }

    [Test]
    public async Task Hash_distinguishes_distinct_inputs()
    {
        var a = RowHasher.Hash("row one");
        var b = RowHasher.Hash("row two");

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Hash_of_empty_string_is_sha256_empty()
    {
        // SHA-256 of the empty string in lowercase hex.
        const string ExpectedEmptyHash =
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var hash = RowHasher.Hash(string.Empty);

        await Assert.That(hash).IsEqualTo(ExpectedEmptyHash);
    }
}
