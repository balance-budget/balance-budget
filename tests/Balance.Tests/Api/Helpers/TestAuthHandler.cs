using System.Security.Claims;
using System.Text.Encodings.Web;
using Balance.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balance.Tests.Api.Helpers;

/// <summary>
/// Integration-test auth handler: every request becomes a synthetic authenticated user.
/// The handler emits the principal with <c>AuthenticationType = AuthSchemes.ApiToken</c>
/// so the production AntiforgeryEndpointFilter naturally treats it as a token-auth
/// request and skips antiforgery — exactly what we want when the existing tests post
/// raw JSON without an XSRF cookie.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";
    public static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim(ClaimTypes.Name, "tester@example.com"),
            new Claim(ClaimTypes.Email, "tester@example.com"),
        };
        var identity = new ClaimsIdentity(claims, AuthSchemes.ApiToken);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
