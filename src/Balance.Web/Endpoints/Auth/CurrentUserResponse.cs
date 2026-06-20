using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Auth;

internal sealed record CurrentUserResponse(
    UserId Id,
    string Email,
    string DisplayName,
    string AuthScheme,
    string? Language,
    string? DateFormat,
    string? NumberFormat,
    string? Theme
);
