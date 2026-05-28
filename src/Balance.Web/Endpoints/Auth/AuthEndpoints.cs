using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Balance.Configuration.Options;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Web.Auth;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Balance.Web.Endpoints.Auth;

internal static class AuthEndpoints
{
    public const string PathPrefix = "/auth";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Auth");

        group
            .MapPost("/setup", SetupAsync)
            .AllowAnonymous()
            .WithValidation<SetupRequest>()
            .WithName("Setup");

        group
            .MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithValidation<LoginRequest>()
            .WithName("Login");

        group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Logout");

        group.MapGet("/me", MeAsync).WithName("Me");
    }

    public static void MapAntiforgery(this IEndpointRouteBuilder app)
    {
        app.MapGet("/antiforgery/token", AntiforgeryTokenAsync)
            .AllowAnonymous()
            .WithTags("Auth")
            .WithName("AntiforgeryToken");
    }

    private static async Task<
        Results<NoContent, NotFound, ValidationProblem, BadRequest<ProblemDetails>>
    > SetupAsync(
        [FromBody] SetupRequest request,
        [FromServices] BalanceDbContext db,
        [FromServices] UserManager<BalanceUser> userManager,
        [FromServices] IOptions<AuthOptions> authOptions,
        [FromServices] TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        // Empty-table guard first: once a user exists, the wizard is gone for good — and
        // it 404s rather than 409s so probing the URL does not reveal the machinery
        // ever existed (ADR 0018).
        var anyUser = await db.Users.AnyAsync(cancellationToken);
        if (anyUser)
        {
            return TypedResults.NotFound();
        }

        // SetupToken guard. If configured, must match exactly. Constant-time compare.
        // If not configured, accept any value (development convenience — the empty-table
        // guard is still in force).
        var expectedToken = authOptions.Value.SetupToken;
        if (!string.IsNullOrEmpty(expectedToken))
        {
            if (string.IsNullOrEmpty(request.SetupToken))
            {
                return TypedResults.NotFound();
            }
            var expected = Encoding.UTF8.GetBytes(expectedToken);
            var actual = Encoding.UTF8.GetBytes(request.SetupToken);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return TypedResults.NotFound();
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new BalanceUser
        {
            Id = new UserId(Guid.CreateVersion7()),
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName,
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return TypedResults.BadRequest(
                new ProblemDetails
                {
                    Title = "Setup failed",
                    Detail = string.Join(
                        "; ",
                        createResult.Errors.Select(e => $"{e.Code}: {e.Description}")
                    ),
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        // Sign the new user in immediately so the SPA can navigate straight to the app.
        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            BuildCookiePrincipal(user)
        );
        return TypedResults.NoContent();
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, ValidationProblem>
    > LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] UserManager<BalanceUser> userManager,
        [FromServices] SignInManager<BalanceUser> signInManager,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true
        );
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            BuildCookiePrincipal(user)
        );
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LogoutAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(
        HttpContext httpContext,
        [FromServices] UserManager<BalanceUser> userManager
    )
    {
        if (httpContext.User.Identity is not { IsAuthenticated: true } identity)
        {
            return TypedResults.Unauthorized();
        }

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userIdValue))
        {
            return TypedResults.Unauthorized();
        }

        var userId = new UserId(userIdValue);
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(
            new CurrentUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                identity.AuthenticationType ?? string.Empty
            )
        );
    }

    private static NoContent AntiforgeryTokenAsync(
        HttpContext httpContext,
        [FromServices] IAntiforgery antiforgery
    )
    {
        // Issues the XSRF-TOKEN cookie (JS-readable) and stores the matching request token.
        // The SPA reads the cookie value and echoes it as the X-XSRF-TOKEN header on writes.
        antiforgery.GetAndStoreTokens(httpContext);
        return TypedResults.NoContent();
    }

    private static ClaimsPrincipal BuildCookiePrincipal(BalanceUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        return new ClaimsPrincipal(identity);
    }
}
