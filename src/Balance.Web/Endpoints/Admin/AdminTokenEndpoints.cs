using System.Security.Claims;
using System.Security.Cryptography;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Web.Auth;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Balance.Web.Endpoints.Admin;

internal static class AdminTokenEndpoints
{
    public const string PathPrefix = "/admin/tokens";

    public static void MapAdminTokens(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Admin / Tokens").RequireAuthorization();
        group.MapGet("", ListAsync).WithName("ListTokens");
        group.MapPost("", CreateAsync).WithValidation<CreateTokenRequest>().WithName("CreateToken");
        group.MapPost("/{id}/revoke", RevokeAsync).WithName("RevokeToken");
    }

    private static async Task<Ok<IReadOnlyList<TokenResponse>>> ListAsync(
        [FromServices] BalanceDbContext db,
        CancellationToken cancellationToken
    )
    {
        var tokens = await db
            .ApiTokens.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TokenResponse(
                t.Id,
                t.UserId,
                t.Name,
                t.Prefix,
                t.Last4,
                t.CreatedAt,
                t.LastUsedAt,
                t.ExpiresAt,
                t.RevokedAt
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TokenResponse>>(tokens);
    }

    private static async Task<
        Results<Created<CreatedTokenResponse>, UnauthorizedHttpResult, ValidationProblem>
    > CreateAsync(
        [FromBody] CreateTokenRequest request,
        [FromServices] BalanceDbContext db,
        [FromServices] TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var userId = CurrentUserId(httpContext);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // 32 bytes = 256 bits of entropy; encoded as base64url for a URL/header-safe value.
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Base64UrlEncode(secretBytes);
        var plaintext = $"{AuthSchemes.ApiTokenPrefix}{secret}";

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = new ApiToken
        {
            Id = new ApiTokenId(Guid.CreateVersion7()),
            UserId = userId.Value,
            Name = request.Name,
            TokenHash = ApiTokenHasher.Hash(plaintext),
            Prefix = AuthSchemes.ApiTokenPrefix,
            Last4 = secret[^4..],
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = request.ExpiresAt,
        };
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        var metadata = new TokenResponse(
            token.Id,
            token.UserId,
            token.Name,
            token.Prefix,
            token.Last4,
            token.CreatedAt,
            token.LastUsedAt,
            token.ExpiresAt,
            token.RevokedAt
        );
        var response = new CreatedTokenResponse(metadata, plaintext);
        return TypedResults.Created($"{PathPrefix}/{token.Id.Value}", response);
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(
        [FromRoute] ApiTokenId id,
        [FromServices] BalanceDbContext db,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (token is null)
        {
            return TypedResults.NotFound();
        }

        if (token.RevokedAt is null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            token.RevokedAt = now;
            token.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }
        return TypedResults.NoContent();
    }

    private static UserId? CurrentUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is not null && Guid.TryParse(claim, out var value))
        {
            return new UserId(value);
        }
        return null;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var standard = Convert.ToBase64String(bytes);
        return standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
