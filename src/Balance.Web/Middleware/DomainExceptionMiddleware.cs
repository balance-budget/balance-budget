using Balance.Data.Exceptions;
using Balance.Web.Logging;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Middleware;

internal sealed class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(
        RequestDelegate next,
        ILogger<DomainExceptionMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.DomainExceptionThrown(ex.Kind, ex.Message);
            await WriteProblemAsync(context, ex);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, DomainException ex)
    {
        var (status, title) = ex.Kind switch
        {
            DomainExceptionKind.Validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed"
            ),
            DomainExceptionKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            DomainExceptionKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Domain invariant violated"),
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = ex.Message,
            Type = $"https://httpstatuses.com/{status}",
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
