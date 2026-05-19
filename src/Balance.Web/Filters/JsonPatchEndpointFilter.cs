using FluentValidation;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Balance.Web.Filters;

/// <summary>
/// Endpoint filter for PATCH endpoints that bind a <see cref="JsonPatchDocument{TInput}"/>.
/// Loads a current entity snapshot via a route-bound factory, applies the patch document
/// collecting apply-time errors, runs the registered <see cref="IValidator{TInput}"/> on
/// the post-patch snapshot, and stashes the validated snapshot in
/// <c>HttpContext.Items</c> under <see cref="SnapshotKey"/> so the handler can pick it up
/// via <see cref="JsonPatchEndpointFilter.GetSnapshot{TInput}"/>.
/// </summary>
internal sealed class JsonPatchEndpointFilter<TInput> : IEndpointFilter
    where TInput : class
{
    public const string SnapshotKey = "__balance.jsonpatch.snapshot__";

    private readonly Func<
        EndpointFilterInvocationContext,
        CancellationToken,
        Task<TInput?>
    > _snapshotFactory;
    private readonly IValidator<TInput>? _validator;

    public JsonPatchEndpointFilter(
        Func<EndpointFilterInvocationContext, CancellationToken, Task<TInput?>> snapshotFactory,
        IValidator<TInput>? validator
    )
    {
        _snapshotFactory = snapshotFactory;
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var ct = context.HttpContext.RequestAborted;

        var patch = context.Arguments.OfType<JsonPatchDocument<TInput>>().FirstOrDefault();
        if (patch is null)
        {
            return Results.Problem(
                title: "Patch document missing",
                detail: $"Expected a JsonPatchDocument<{typeof(TInput).Name}> argument.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var snapshot = await _snapshotFactory(context, ct);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        Dictionary<string, List<string>>? applyErrors = null;
        patch.ApplyTo(
            snapshot,
            error =>
            {
                applyErrors ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var path = error.Operation?.path ?? string.Empty;
                if (!applyErrors.TryGetValue(path, out var messages))
                {
                    messages = new List<string>();
                    applyErrors[path] = messages;
                }
                messages.Add(error.ErrorMessage);
            }
        );

        if (applyErrors is not null)
        {
            var errors = applyErrors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            return Results.ValidationProblem(errors);
        }

        if (_validator is not null)
        {
            var result = await _validator.ValidateAsync(snapshot, ct);
            if (!result.IsValid)
            {
                var errors = result
                    .Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }
        }

        context.HttpContext.Items[SnapshotKey] = snapshot;
        return await next(context);
    }
}

internal static class JsonPatchEndpointFilter
{
    /// <summary>
    /// Reads the validated post-patch snapshot stashed by
    /// <see cref="JsonPatchEndpointFilter{TInput}"/> on the current <see cref="HttpContext"/>.
    /// Throws if the filter didn't run (programmer error — wiring mismatch).
    /// </summary>
    public static TInput GetSnapshot<TInput>(HttpContext httpContext)
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (
            !httpContext.Items.TryGetValue(
                JsonPatchEndpointFilter<TInput>.SnapshotKey,
                out var stored
            ) || stored is not TInput snapshot
        )
        {
            throw new InvalidOperationException(
                $"No JSON Patch snapshot of type {typeof(TInput).Name} found on HttpContext. "
                    + "Ensure WithJsonPatch<TInput>() is wired on the endpoint."
            );
        }
        return snapshot;
    }
}
