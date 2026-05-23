using System.Diagnostics.CodeAnalysis;
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
    /// runs FluentValidation on the result, and persists the change. The
    /// <see cref="JsonPatchTargetMetadata"/> for <typeparamref name="TInput"/> is attached
    /// automatically so the OpenAPI document gets the patchable-paths enum.
    /// </summary>
    public static RouteHandlerBuilder MapPatchSnapshotted<TId, TService, TInput, TOutput>(
        this RouteGroupBuilder group,
        [StringSyntax("Route")] string pattern,
        Func<TService, TId, CancellationToken, Task<Result<TInput>>> getSnapshot,
        Func<TService, TId, TInput, CancellationToken, Task<Result<TOutput>>> update
    )
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
                    [FromRoute] TId id,
                    [FromBody] JsonPatchDocument<TInput> patch,
                    [FromServices] TService service,
                    [FromServices] IValidator<TInput>? validator,
                    CancellationToken cancellationToken
                ) =>
                {
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
