using System.Security.Claims;
using System.Text.Encodings.Web;
using Balance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balance.Web.Auth;

internal sealed class ApiTokenAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var header = authorizationHeader.ToString();
        const string bearerPrefix = "Bearer ";
        if (!header.StartsWith(bearerPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[bearerPrefix.Length..].Trim();
        if (!token.StartsWith(AuthSchemes.ApiTokenPrefix, StringComparison.Ordinal))
        {
            // Not a personal access token — let the policy scheme forward elsewhere.
            return AuthenticateResult.NoResult();
        }

        var hash = ApiTokenHasher.Hash(token);

        var db = Context.RequestServices.GetRequiredService<BalanceDbContext>();
        var timeProvider = Context.RequestServices.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var row = await db
            .ApiTokens.AsTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, Context.RequestAborted);

        if (row is null)
        {
            return AuthenticateResult.Fail("Unknown token.");
        }

        if (row.RevokedAt is not null)
        {
            return AuthenticateResult.Fail("Token has been revoked.");
        }

        if (row.ExpiresAt is { } expiresAt && expiresAt <= now.UtcDateTime)
        {
            return AuthenticateResult.Fail("Token has expired.");
        }

        var user = await db
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == row.UserId, Context.RequestAborted);

        if (user is null)
        {
            return AuthenticateResult.Fail("Token owner is missing.");
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            return AuthenticateResult.Fail("Token owner is locked out.");
        }

        // Best-effort LastUsedAt update — never block the request on failure.
        row.LastUsedAt = now.UtcDateTime;
        row.UpdatedAt = now.UtcDateTime;
        try
        {
            await db.SaveChangesAsync(Context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            // Concurrency or transient — token auth is still valid; just don't update.
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("token_id", row.Id.Value.ToString()),
        };
        var identity = new ClaimsIdentity(claims, AuthSchemes.ApiToken);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthSchemes.ApiToken);
        return AuthenticateResult.Success(ticket);
    }
}
