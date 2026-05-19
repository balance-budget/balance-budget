using Balance.Data.Exceptions;
using Balance.Web.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Middleware;

internal sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<DomainExceptionHandler> logger
    )
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainException domain)
        {
            return false;
        }

        _logger.DomainExceptionThrown(domain.Kind, domain.Message);

        var (status, title) = domain.Kind switch
        {
            DomainExceptionKind.Validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed"
            ),
            DomainExceptionKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            DomainExceptionKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Domain invariant violated"),
        };

        httpContext.Response.StatusCode = status;

        ProblemDetails problemDetails =
            domain.Kind == DomainExceptionKind.Validation && domain.Errors is not null
                ? new ValidationProblemDetails(
                    domain.Errors.ToDictionary(kv => kv.Key, kv => kv.Value)
                )
                {
                    Status = status,
                    Title = title,
                    Detail = domain.Message,
                }
                : new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = domain.Message,
                };

        return await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception,
            }
        );
    }
}
