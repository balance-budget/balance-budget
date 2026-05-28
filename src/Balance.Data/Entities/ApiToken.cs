using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class ApiToken : BaseEntity<ApiTokenId>
{
    public required UserId UserId { get; init; }
    public required string Name { get; set; }
    public required string TokenHash { get; init; }
    public required string Prefix { get; init; }
    public required string Last4 { get; init; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; set; }
}
