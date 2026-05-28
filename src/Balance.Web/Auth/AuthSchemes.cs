namespace Balance.Web.Auth;

internal static class AuthSchemes
{
    /// <summary>
    /// The default scheme registered on AddAuthentication — a policy scheme that sniffs
    /// the <c>Authorization</c> header and forwards to either <see cref="ApiToken"/> or
    /// the cookie scheme (Identity's application cookie).
    /// </summary>
    public const string Selector = "Balance.AuthSelector";

    /// <summary>
    /// Custom scheme name for the <c>bal_pat_*</c> personal access token handler.
    /// </summary>
    public const string ApiToken = "Balance.ApiToken";

    /// <summary>
    /// Prefix that identifies a personal access token on the wire. The policy scheme
    /// uses this prefix on <c>Authorization: Bearer ...</c> to decide whether to forward
    /// to the token handler or to the cookie handler.
    /// </summary>
    public const string ApiTokenPrefix = "bal_pat_";

    /// <summary>
    /// Cookie name used for SPA sessions. Distinct from the default
    /// <c>.AspNetCore.Identity.Application</c> so it is obvious in DevTools.
    /// </summary>
    public const string CookieName = "balance.auth";
}
