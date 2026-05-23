using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Balance.Web.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints;

internal static class PatchSnapshottedEndpointExtensions
{
    /// <summary>
    /// Registers a JSON Patch endpoint that loads a typed snapshot, applies the patch,
    /// runs FluentValidation on the result, and persists the change. The id is read from
    /// the route value named <paramref name="idRouteName"/> (default <c>"id"</c>) and parsed
    /// via <see cref="IParsable{TId}"/> so it works with any typed ID. The
    /// <see cref="JsonPatchTargetMetadata"/> for <typeparamref name="TInput"/> is attached
    /// automatically so the OpenAPI document gets the patchable-paths enum.
    /// </summary>
    public static RouteHandlerBuilder MapPatchSnapshotted<TId, TService, TInput, TOutput>(
        this RouteGroupBuilder group,
        [StringSyntax("Route")] string pattern,
        Func<TService, TId, CancellationToken, Task<Result<TInput>>> getSnapshot,
        Func<TService, TId, TInput, CancellationToken, Task<Result<TOutput>>> update,
        string idRouteName = "id"
    )
        where TId : IParsable<TId>
        where TService : notnull
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(getSnapshot);
        ArgumentNullException.ThrowIfNull(update);

        return group
            .MapPatch(
                pattern,
                async (
                    HttpContext httpContext,
                    [FromBody] JsonPatchDocument<TInput> patch,
                    [FromServices] TService service,
                    [FromServices] IValidator<TInput>? validator,
                    CancellationToken cancellationToken
                ) =>
                {
                    var raw = httpContext.GetRouteValue(idRouteName) as string;
                    if (raw is null || !TId.TryParse(raw, CultureInfo.InvariantCulture, out var id))
                    {
                        return new Result<TOutput>(
                            new NotFoundError(typeof(TOutput).Name, raw ?? string.Empty)
                        ).ToOk();
                    }

                    var snapshot = await getSnapshot(service, id, cancellationToken);
                    if (snapshot.IsFailure)
                        return new Result<TOutput>(snapshot.Error).ToOk();

                    var patched = await patch.ApplyAndValidateAsync(
                        snapshot.Value,
                        validator,
                        cancellationToken
                    );
                    if (patched.IsFailure)
                        return new Result<TOutput>(patched.Error).ToOk();

                    var result = await update(service, id, patched.Value, cancellationToken);
                    return result.ToOk();
                }
            )
            .WithJsonPatchTarget<TInput>();
    }
}
