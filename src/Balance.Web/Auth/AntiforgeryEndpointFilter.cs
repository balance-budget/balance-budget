using Microsoft.AspNetCore.Antiforgery;

namespace Balance.Web.Auth;

/// <summary>
/// Endpoint filter that enforces antiforgery on cookie-authenticated mutations and
/// transparently lets PAT-authenticated calls through (ADR 0018: CSRF is a browser-only
/// concern). Endpoints that explicitly call <c>.DisableAntiforgery()</c> are honoured.
/// </summary>
internal class AntiforgeryEndpointFilter : IEndpointFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    private readonly IAntiforgery _antiforgery;

    public AntiforgeryEndpointFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public virtual async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        if (SafeMethods.Contains(httpContext.Request.Method))
        {
            return await next(context).ConfigureAwait(false);
        }

        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: false })
        {
            // Endpoint opted out via .DisableAntiforgery() — for example the multipart
            // statement-import endpoint.
            return await next(context).ConfigureAwait(false);
        }

        if (
            httpContext.User.Identity is { IsAuthenticated: true } identity
            && string.Equals(
                identity.AuthenticationType,
                AuthSchemes.ApiToken,
                StringComparison.Ordinal
            )
        )
        {
            return await next(context).ConfigureAwait(false);
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "Invalid antiforgery token",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        return await next(context).ConfigureAwait(false);
    }
}
