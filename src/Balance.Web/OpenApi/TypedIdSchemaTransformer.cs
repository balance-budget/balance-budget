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
internal sealed class TypedIdSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly HashSet<Type> GuidBackedIds = TypedIdsWithUnderlyingType<Guid>();
    private static readonly HashSet<Type> StringBackedIds = TypedIdsWithUnderlyingType<string>();

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
