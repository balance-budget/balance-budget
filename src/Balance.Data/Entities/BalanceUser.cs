using Balance.Data.Entities.Ids;
using Microsoft.AspNetCore.Identity;

namespace Balance.Data.Entities;

public sealed class BalanceUser : IdentityUser<UserId>
{
    public required string DisplayName { get; set; }

    // Display preferences (ADR-0022). Opaque tokens the backend persists but never
    // interprets; null means "use the SPA default". Language drives translated
    // strings, DateFormat/NumberFormat drive client-side formatting only.
    public string? Language { get; set; }

    public string? DateFormat { get; set; }

    public string? NumberFormat { get; set; }
}
