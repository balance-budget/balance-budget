using Balance.Configuration.Contracts;

namespace Balance.Configuration.Options;

public sealed class AuthOptions : IOptionsSection
{
    public static string Section => "Auth";

    /// <summary>
    /// Deploy-time secret guarding the first-run setup wizard. The wizard refuses requests
    /// unless this token is supplied (header or query), <em>and</em> the user table is empty
    /// (ADR 0018). May be null in non-production environments; the wizard then accepts any
    /// (or absent) token, since the empty-table guard is still in effect.
    /// </summary>
    public string? SetupToken { get; init; }

    /// <summary>
    /// SameSite policy for the auth cookie. Default: <c>Strict</c> (ADR 0018) — this is a
    /// self-hosted personal app, no marketing email deep-links, and Strict still works for
    /// bookmarks and typed URLs (top-level user-initiated navigations send the cookie).
    /// </summary>
    public string CookieSameSite { get; init; } = "Strict";

    /// <summary>
    /// Whether the auth cookie should be marked Secure. Default: <c>true</c>.
    /// Set to <c>false</c> for local non-HTTPS development.
    /// </summary>
    public bool CookieSecure { get; init; } = true;
}
