using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balance.Web.OpenApi;

/// <summary>
/// Makes sure that all responses with a <see cref="ProblemDetails"/> type also have an "application/problem+json" content type, as per RFC 7807.
/// This is a temporary workaround until https://github.com/dotnet/aspnetcore/issues/58574 is fixed.
/// </summary>
internal sealed class ProblemDetailsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (operation.Responses is null)
            return Task.CompletedTask;

        var problemStatusCodes = context
            .Description.SupportedResponseTypes.Where(r => r.Type == typeof(ProblemDetails))
            .Select(r => r.StatusCode.ToString(CultureInfo.InvariantCulture))
            .ToHashSet();

        foreach (var statusCode in problemStatusCodes)
        {
            if (
                !operation.Responses.TryGetValue(statusCode, out var response)
                || response.Content is null
                || !response.Content.Remove("application/json", out var mediaType)
            )
            {
                continue;
            }

            response.Content["application/problem+json"] = mediaType;
        }

        return Task.CompletedTask;
    }
}
