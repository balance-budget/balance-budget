using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Web.Filters;

internal static class ValidationEndpointFilterExtensions
{
    /// <summary>
    /// Resolves <see cref="IValidator{TRequest}"/> from DI and validates the bound
    /// <typeparamref name="TRequest"/> argument before the handler runs.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<ValidationEndpointFilter<TRequest>>();
    }

    /// <summary>
    /// Variant for endpoints that don't bind <typeparamref name="TRequest"/> directly.
    /// The factory composes the request from other bound arguments (e.g. route values).
    /// </summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(
        this RouteHandlerBuilder builder,
        Func<EndpointFilterInvocationContext, TRequest> requestFactory
    )
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return builder.AddEndpointFilter(
            async (context, next) =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<
                    IValidator<TRequest>
                >();
                var request = requestFactory(context);
                var result = await validator.ValidateAsync(
                    request,
                    context.HttpContext.RequestAborted
                );
                if (result.IsValid)
                {
                    return await next(context);
                }

                var errors = result
                    .Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }
        );
    }
}
