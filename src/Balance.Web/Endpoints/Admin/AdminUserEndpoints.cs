using System.Security.Claims;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Balance.Web.Endpoints.Admin;

internal static class AdminUserEndpoints
{
    public const string PathPrefix = "/admin/users";

    public static void MapAdminUsers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Admin / Users").RequireAuthorization();
        group.MapGet("", ListAsync).WithName("ListUsers");
        group.MapPost("", CreateAsync).WithValidation<CreateUserRequest>().WithName("CreateUser");
        group.MapPost("/{id}/disable", DisableAsync).WithName("DisableUser");
        group.MapPost("/{id}/enable", EnableAsync).WithName("EnableUser");
    }

    private static async Task<Ok<IReadOnlyList<UserResponse>>> ListAsync(
        [FromServices] BalanceDbContext db,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        var users = await db
            .Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new UserResponse(
                u.Id,
                u.Email ?? string.Empty,
                u.DisplayName,
                u.LockoutEnd == null || u.LockoutEnd <= now,
                u.LockoutEnd
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<UserResponse>>(users);
    }

    private static async Task<
        Results<Created<UserResponse>, BadRequest<ProblemDetails>, ValidationProblem>
    > CreateAsync(
        [FromBody] CreateUserRequest request,
        [FromServices] UserManager<BalanceUser> userManager,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var user = new BalanceUser
        {
            Id = new UserId(Guid.CreateVersion7()),
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName,
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(
                new ProblemDetails
                {
                    Title = "User creation failed",
                    Detail = string.Join(
                        "; ",
                        result.Errors.Select(e => $"{e.Code}: {e.Description}")
                    ),
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        var response = new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            IsActive: true,
            LockoutEnd: null
        );
        return TypedResults.Created($"{PathPrefix}/{user.Id.Value}", response);
    }

    private static async Task<
        Results<NoContent, NotFound, BadRequest<ProblemDetails>>
    > DisableAsync(
        [FromRoute] UserId id,
        [FromServices] BalanceDbContext db,
        [FromServices] UserManager<BalanceUser> userManager,
        [FromServices] TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = CurrentUserId(httpContext);
        if (currentUserId == id)
        {
            return TypedResults.BadRequest(
                new ProblemDetails
                {
                    Title = "Cannot disable yourself",
                    Detail = "You cannot disable the account you are currently signed in as.",
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        var now = timeProvider.GetUtcNow();
        var activeCount = await db.Users.CountAsync(
            u => u.LockoutEnd == null || u.LockoutEnd <= now,
            cancellationToken
        );
        if (activeCount <= 1)
        {
            return TypedResults.BadRequest(
                new ProblemDetails
                {
                    Title = "Cannot disable the last active user",
                    Detail = "At least one user must remain active.",
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        // Bump the security stamp so existing cookie sessions get rejected on next validation.
        await userManager.UpdateSecurityStampAsync(user);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> EnableAsync(
        [FromRoute] UserId id,
        [FromServices] UserManager<BalanceUser> userManager,
        CancellationToken cancellationToken
    )
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
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
}
