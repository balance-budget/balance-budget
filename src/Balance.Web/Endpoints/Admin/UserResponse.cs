using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Admin;

internal sealed record UserResponse(
    UserId Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LockoutEnd
);
