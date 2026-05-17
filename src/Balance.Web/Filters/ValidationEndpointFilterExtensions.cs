using FluentValidation;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// Wires the <see cref="JsonPatchEndpointFilter{TInput}"/> for an endpoint that binds a
    /// <c>JsonPatchDocument&lt;TInput&gt;</c>. The <paramref name="snapshotFactory"/> loads
    /// the current entity state (or returns null for 404). The filter applies the patch,
    /// runs the registered <see cref="IValidator{TInput}"/> if any, and then forwards the
    /// validated snapshot to the handler in place of the patch document argument.
    /// </summary>
    public static RouteHandlerBuilder WithJsonPatch<TInput>(
        this RouteHandlerBuilder builder,
        Func<EndpointFilterInvocationContext, CancellationToken, Task<TInput?>> snapshotFactory
    )
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(snapshotFactory);

        return builder.AddEndpointFilterFactory(
            (_, next) =>
            {
                return async context =>
                {
                    var validator = context.HttpContext.RequestServices.GetService<
                        IValidator<TInput>
                    >();
                    var filter = new JsonPatchEndpointFilter<TInput>(snapshotFactory, validator);
                    return await filter.InvokeAsync(context, next);
                };
            }
        );
    }
}
