using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balance.Web.OpenApi;

/// <summary>
/// For PATCH endpoints carrying <see cref="JsonPatchTargetMetadata"/>, registers a
/// per-target <c>JsonPatchDocumentOf{TargetName}</c> schema in <c>components.schemas</c>
/// and rewrites the request body to reference it. The per-target schema mirrors the
/// framework's shared <c>JsonPatchDocument</c> shape but constrains the <c>path</c> field
/// to an enum of the top-level patchable properties of the target type, so consumers'
/// codegen narrows <c>path</c> to a string-literal union.
/// </summary>
internal sealed class JsonPatchOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var target = context
            .Description.ActionDescriptor.EndpointMetadata.OfType<JsonPatchTargetMetadata>()
            .FirstOrDefault();

        if (target is null)
            return;

        var targetSchema = await context.GetOrCreateSchemaAsync(
            target.Target,
            cancellationToken: cancellationToken
        );

        operation.Description =
            $"Applies a JSON Patch (RFC 6902) document. The patchable surface is described "
            + $"by `{target.Target.Name}`. The `path` enum lists the top-level patchable "
            + "properties only; dictionary-valued properties (e.g. keyed line items) accept "
            + "child paths like `/lines/{key}/description` at runtime but are not enumerated "
            + "here. Consumers' codegen will not narrow those.";

        var paths = GetTopLevelPaths(target.Target);
        if (paths.Length == 0 || operation.RequestBody?.Content is not { } content)
            return;

        var document = context.Document;
        if (document is null)
            return;

        var components = document.Components ?? (document.Components = new OpenApiComponents());
        var schemas =
            components.Schemas
            ?? (
                components.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            );

        // Ensure the target shape is in components so consumers can navigate to it.
        if (!schemas.ContainsKey(target.Target.Name))
            schemas[target.Target.Name] = targetSchema;

        var schemaName = "JsonPatchDocumentOf" + target.Target.Name;
        if (!schemas.ContainsKey(schemaName))
            schemas[schemaName] = BuildPatchSchema(paths);

        foreach (var media in content.Values)
        {
            media.Schema = new OpenApiSchemaReference(schemaName, document);
        }
    }

    private static string[] GetTopLevelPaths(Type target) =>
        target
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(p =>
            {
                var name =
                    p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? JsonNamingPolicy.CamelCase.ConvertName(p.Name);
                return "/" + name;
            })
            .ToArray();

    private static OpenApiSchema BuildPatchSchema(string[] paths)
    {
        var pathEnum = paths.Select(p => (JsonNode)JsonValue.Create(p)).ToList();

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                OneOf =
                [
                    BuildBranch(
                        ops: ["add", "replace", "test"],
                        required: ["op", "path", "value"],
                        pathEnum: pathEnum,
                        extraProperties: new Dictionary<string, IOpenApiSchema>(
                            StringComparer.Ordinal
                        )
                        {
                            ["value"] = new OpenApiSchema(),
                        }
                    ),
                    BuildBranch(
                        ops: ["move", "copy"],
                        required: ["op", "path", "from"],
                        pathEnum: pathEnum,
                        extraProperties: new Dictionary<string, IOpenApiSchema>(
                            StringComparer.Ordinal
                        )
                        {
                            ["from"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        }
                    ),
                    BuildBranch(
                        ops: ["remove"],
                        required: ["op", "path"],
                        pathEnum: pathEnum,
                        extraProperties: null
                    ),
                ],
            },
        };
    }

    private static OpenApiSchema BuildBranch(
        string[] ops,
        string[] required,
        List<JsonNode> pathEnum,
        IDictionary<string, IOpenApiSchema>? extraProperties
    )
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["op"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. ops.Select(o => (JsonNode)JsonValue.Create(o))],
            },
            ["path"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = pathEnum },
        };

        if (extraProperties is not null)
        {
            foreach (var (key, value) in extraProperties)
            {
                properties[key] = value;
            }
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string>(required, StringComparer.Ordinal),
            Properties = properties,
        };
    }
}
