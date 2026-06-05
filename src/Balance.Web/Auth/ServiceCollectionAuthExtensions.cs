using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;

namespace Balance.Web.Auth;

internal static class ServiceCollectionAuthExtensions
{
    public static IServiceCollection AddBalanceAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        _ = configuration;

        // SignInManager lives on the web side because it brings in scheme dependencies (the cookie
        // scheme handler etc.) that Balance.Data does not reference. IdentityCore itself is already
        // registered (with the password / lockout policy) by AddBalanceData, so attach SignInManager
        // to a fresh IdentityBuilder rather than calling AddIdentityCore a second time.
        new IdentityBuilder(typeof(Data.Entities.BalanceUser), services).AddSignInManager();

        // Disable a user (LockoutEnd > now) takes effect on existing SPA sessions within
        // ~5 minutes — short enough to be operationally usable, long enough to avoid a DB
        // roundtrip on every request (ADR 0016).
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(5);
        });

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = AuthSchemes.Selector;
                options.DefaultAuthenticateScheme = AuthSchemes.Selector;
                options.DefaultChallengeScheme = AuthSchemes.Selector;
            })
            .AddPolicyScheme(
                AuthSchemes.Selector,
                "Selector",
                policy =>
                {
                    policy.ForwardDefaultSelector = static context =>
                    {
                        if (
                            context.Request.Headers.TryGetValue("Authorization", out var header)
                            && header.ToString() is { } value
                            && value.StartsWith("Bearer ", StringComparison.Ordinal)
                            && value
                                .AsSpan(7)
                                .TrimStart()
                                .StartsWith(AuthSchemes.ApiTokenPrefix, StringComparison.Ordinal)
                        )
                        {
                            return AuthSchemes.ApiToken;
                        }
                        return IdentityConstants.ApplicationScheme;
                    };
                }
            )
            .AddCookie(
                IdentityConstants.ApplicationScheme,
                cookie =>
                {
                    cookie.Cookie.Name = AuthSchemes.CookieName;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SameSite = SameSiteMode.Strict;
                    // Secure in production (ADR 0016); SameAsRequest in dev so the plain-HTTP
                    // localhost host still sets the cookie.
                    cookie.Cookie.SecurePolicy = environment.IsProduction()
                        ? CookieSecurePolicy.Always
                        : CookieSecurePolicy.SameAsRequest;
                    cookie.ExpireTimeSpan = TimeSpan.FromDays(30);
                    cookie.SlidingExpiration = true;
                    cookie.Events = CookieAuthenticationEvents();
                }
            )
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
                AuthSchemes.ApiToken,
                _ => { }
            );

        return services;
    }

    /// <summary>
    /// Replace the default redirect-to-/Account/Login behaviour with bare 401/403 responses —
    /// the SPA handles the redirect through its TanStack Query global error handler.
    /// </summary>
    private static CookieAuthenticationEvents CookieAuthenticationEvents() =>
        new()
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
}
