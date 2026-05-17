using Balance.Data.Entities.Ids;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balance.Web.OpenApi;

/// <summary>
/// Flattens StronglyTypedId record-struct types in the OpenAPI document to their primitive
/// JSON representation: <c>{ type: "string", format: "uuid" }</c> for Guid-backed IDs and
/// <c>{ type: "string" }</c> for <see cref="CurrencyCode"/>. Without this transformer the
/// schema would expose a wrapper object with a single <c>Value</c> property.
/// </summary>
internal sealed class TypedIdSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly HashSet<Type> GuidBackedIds =
    [
        typeof(AccountId),
        typeof(BankAccountId),
        typeof(BankTransactionId),
        typeof(CounterpartyId),
        typeof(JournalEntryId),
        typeof(JournalLineId),
    ];

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
        else if (type == typeof(CurrencyCode))
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
}
