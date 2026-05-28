using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Admin;

internal sealed record TokenResponse(
    ApiTokenId Id,
    UserId UserId,
    string Name,
    string Prefix,
    string Last4,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt
);

/// <summary>
/// Returned exactly once when minting a token. The <see cref="Token"/> field carries the
/// plaintext value; it is not recoverable after this response is sent.
/// </summary>
internal sealed record CreatedTokenResponse(TokenResponse Metadata, string Token);
