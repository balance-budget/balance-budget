using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Auth;

/// <summary>
/// Variant of <see cref="Helpers.WebApplicationFactory"/> that does <em>not</em> install
/// the TestAuth bypass scheme — used by auth-flow tests that need to exercise the real
/// cookie / PAT machinery end-to-end. The setup token is configured via
/// <see cref="RealAuthEndpointsTestsBase.ConfigureTestConfiguration"/>; the cookie
/// scheme is patched here to drop <c>Secure</c> so the in-process HTTP test client
/// preserves it across requests.
/// </summary>
internal sealed class RealAuthWebApplicationFactory : TestWebApplicationFactory<Program>
{
    public const string SetupToken = "test-setup-token-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("IntegrationTest");
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme,
                opts =>
                {
                    opts.Cookie.SecurePolicy = CookieSecurePolicy.None;
                    opts.Cookie.SameSite = SameSiteMode.Lax;
                }
            );
        });
    }
}
