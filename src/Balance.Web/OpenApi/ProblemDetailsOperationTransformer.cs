using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balance.Web.OpenApi;

internal sealed class ProblemDetailsOperationTransformer : IOpenApiOperationTransformer
{
    private const string ContentType = "application/problem+json";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        operation.Responses ??= new OpenApiResponses();

        var problemSchema = await context.GetOrCreateSchemaAsync(
            typeof(ProblemDetails),
            cancellationToken: cancellationToken
        );
        var validationProblemSchema = await context.GetOrCreateSchemaAsync(
            typeof(HttpValidationProblemDetails),
            cancellationToken: cancellationToken
        );

        AddResponse(operation.Responses, "400", "Validation failed", validationProblemSchema);
        AddResponse(operation.Responses, "404", "Not found", problemSchema);
        AddResponse(operation.Responses, "409", "Conflict", problemSchema);
        AddResponse(operation.Responses, "422", "Domain invariant violated", problemSchema);
    }

    private static void AddResponse(
        OpenApiResponses responses,
        string statusCode,
        string description,
        IOpenApiSchema schema
    )
    {
        if (responses.ContainsKey(statusCode))
        {
            return;
        }

        responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [ContentType] = new OpenApiMediaType { Schema = schema },
            },
        };
    }
}
