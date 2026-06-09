using System.Security.Cryptography;
using System.Text;

namespace Balance.Services.BankTransactions;

/// <summary>
/// Hashes a statement row's raw bytes per ADR 0009: normalize <c>\r\n</c> to <c>\n</c>,
/// trim trailing whitespace on each line, then SHA-256 the UTF-8 bytes and lowercase-hex the digest.
/// </summary>
public static class RowHasher
{
    public static string Hash(string rawSource)
    {
        ArgumentNullException.ThrowIfNull(rawSource);

        var normalized = Normalize(rawSource);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var digest = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(digest);
    }

    public static string Normalize(string rawSource)
    {
        ArgumentNullException.ThrowIfNull(rawSource);

        var unified = rawSource.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = unified.Split('\n');
        var sb = new StringBuilder(unified.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                sb.Append('\n');

            sb.Append(lines[i].AsSpan().TrimEnd());
        }
        return sb.ToString();
    }
}
