namespace Balance.Web.OpenApi;

/// <summary>
/// Endpoint metadata identifying the target type of a JSON Patch endpoint. Consumed by
/// <see cref="JsonPatchOperationTransformer"/> to enrich the OpenAPI document with the
/// patchable surface (target schema in components + <c>path</c> enum on the operation).
/// </summary>
internal sealed record JsonPatchTargetMetadata(Type Target);

internal static class JsonPatchTargetExtensions
{
    /// <summary>
    /// Marks a JSON Patch endpoint with the type that the document patches. Use on
    /// <c>MapPatch</c> registrations whose handler binds <c>JsonPatchDocument&lt;T&gt;</c>.
    /// </summary>
    public static RouteHandlerBuilder WithJsonPatchTarget<T>(this RouteHandlerBuilder builder)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new JsonPatchTargetMetadata(typeof(T)));
    }
}
