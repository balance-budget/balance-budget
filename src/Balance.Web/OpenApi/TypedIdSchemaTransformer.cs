using Balance.Data.Entities.Ids;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balance.Web.OpenApi;

/// <summary>
/// Flattens StronglyTypedId record-struct types in the OpenAPI document to their primitive
/// JSON representation: <c>{ type: "string", format: "uuid" }</c> for Guid-backed IDs and
/// <c>{ type: "string" }</c> for string-backed IDs (e.g. <see cref="CurrencyCode"/>). Without
/// this transformer the schema would expose a wrapper object with a single <c>Value</c>
/// property. The set of typed IDs is discovered by reflecting over the
/// <c>Balance.Data.Entities.Ids</c> namespace, so new IDs are picked up automatically.
/// </summary>
/// <remarks>
/// Implemented as both an <see cref="IOpenApiSchemaTransformer"/> (per-type pass during
/// schema generation) and an <see cref="IOpenApiDocumentTransformer"/> (final sweep over
/// <c>components.schemas</c>). The document-level sweep is the backstop: the per-type pass
/// can be bypassed when other transformers (notably <see cref="JsonPatchOperationTransformer"/>)
/// call <c>GetOrCreateSchemaAsync</c> for a containing type, which registers nested typed-ID
/// components without re-running the schema transformer pipeline.
/// </remarks>
internal sealed class TypedIdSchemaTransformer
    : IOpenApiSchemaTransformer,
        IOpenApiDocumentTransformer
{
    private static readonly HashSet<Type> GuidBackedIds = TypedIdsWithUnderlyingType<Guid>();
    private static readonly HashSet<Type> StringBackedIds = TypedIdsWithUnderlyingType<string>();
    private static readonly HashSet<string> GuidBackedIdNames = GuidBackedIds
        .Select(t => t.Name)
        .ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> StringBackedIdNames = StringBackedIds
        .Select(t => t.Name)
        .ToHashSet(StringComparer.Ordinal);

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var type = context.JsonTypeInfo.Type;
        if (GuidBackedIds.Contains(type))
        {
            FlattenToString(schema, format: "uuid");
        }
        else if (StringBackedIds.Contains(type))
        {
            FlattenToString(schema, format: null);
        }

        return Task.CompletedTask;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        var schemas = document.Components?.Schemas;
        if (schemas is null)
            return Task.CompletedTask;

        foreach (var (name, schema) in schemas)
        {
            if (schema is not OpenApiSchema mutable)
                continue;
            if (GuidBackedIdNames.Contains(name))
            {
                FlattenToString(mutable, format: "uuid");
            }
            else if (StringBackedIdNames.Contains(name))
            {
                FlattenToString(mutable, format: null);
            }
        }

        return Task.CompletedTask;
    }

    private static void FlattenToString(OpenApiSchema schema, string? format)
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = format;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
    }

    private static HashSet<Type> TypedIdsWithUnderlyingType<T>() =>
        typeof(AccountId)
            .Assembly.GetTypes()
            .Where(t =>
                t.Namespace == typeof(AccountId).Namespace
                && t.IsValueType
                && t.GetProperty("Value")?.PropertyType == typeof(T)
            )
            .ToHashSet();
}
