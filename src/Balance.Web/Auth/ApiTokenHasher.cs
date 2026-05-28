using System.Security.Cryptography;
using System.Text;

namespace Balance.Web.Auth;

/// <summary>
/// SHA-256 hashing for personal access tokens. The wire format <c>bal_pat_&lt;random&gt;</c>
/// carries 256 bits of cryptographically-random server-generated entropy, so a fast hash
/// (SHA-256) is sufficient (ADR 0018 rejected password-style KDFs because there is no
/// low-entropy input to brute force).
/// </summary>
internal static class ApiTokenHasher
{
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
