using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api.Auth;

internal abstract class RealAuthEndpointsTestsBase
    : IsolatedDatabaseTest<RealAuthWebApplicationFactory>
{
    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings["Auth:SetupToken"] = RealAuthWebApplicationFactory.SetupToken;
        settings["Auth:CookieSecure"] = "false";
        settings["Auth:CookieSameSite"] = "Lax";
    }
}
