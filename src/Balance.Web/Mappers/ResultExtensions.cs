using System.Diagnostics;
using Balance.Services.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Mappers;

/// <summary>
/// Maps a service-layer <see cref="Result{T}"/> / <see cref="Result"/> onto a typed
/// <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}"/> union so the
/// minimal API source generator picks up the response shapes for OpenAPI automatically.
/// All errors carry a <c>ProblemDetails</c> payload with a stable <c>code</c> extension
/// and the correct <c>application/problem+json</c> content type.
/// </summary>
internal static class ResultExtensions
{
    public static Results<Ok<T>, ProblemHttpResult, ValidationProblem> ToOk<T>(
        this Result<T> result
    ) => Map(result, TypedResults.Ok);

    /// <summary>
    /// For read endpoints whose only failure mode is "not found" — narrower OpenAPI union than
    /// the full <see cref="ToOk{T}"/> mapping. Use for <c>GET /resource/{id}</c>-style endpoints.
    /// </summary>
    public static Results<Ok<T>, NotFound<ProblemDetails>> ToOkReadOnly<T>(this Result<T> result) =>
        result.ToOk() switch
        {
            Ok<T> o => o,
            NotFound<ProblemDetails> n => n,
            _ => throw new UnreachableException(),
        };

    public static Results<Created<T>, ProblemHttpResult, ValidationProblem> ToCreated<T>(
        this Result<T> result,
        Func<T, string> locationFactory
    ) =>
        Map(
            result,
            value => TypedResults.Created(new Uri(locationFactory(value), UriKind.Relative), value)
        );

    public static Results<NoContent, ProblemHttpResult, ValidationProblem> ToNoContent(
        this Result result
    ) => Map(result, TypedResults.NoContent);

    private static Results<
        TSuccessHttpResult,
        ProblemHttpResult,
        ValidationProblem
    > Map<TSuccessHttpResult>(Result result, Func<TSuccessHttpResult> onSuccess)
        where TSuccessHttpResult : IResult =>
        result.Error is null ? onSuccess() : MapError<TSuccessHttpResult>(result.Error);

    private static Results<TSuccessHttpResult, ProblemHttpResult, ValidationProblem> Map<
        TServiceResult,
        TSuccessHttpResult
    >(Result<TServiceResult> result, Func<TServiceResult, TSuccessHttpResult> onSuccess)
        where TSuccessHttpResult : IResult =>
        result.Error is null
            ? onSuccess(result.Value!)
            : MapError<TSuccessHttpResult>(result.Error);

    private static Results<
        TSuccessHttpResult,
        ProblemHttpResult,
        ValidationProblem
    > MapError<TSuccessHttpResult>(ServiceError error)
        where TSuccessHttpResult : IResult =>
        error switch
        {
            NotFoundError n => TypedResults.Problem(
                Problem(StatusCodes.Status404NotFound, n.Code, $"{n.Entity} {n.Id} not found.", n)
            ),
            ConflictError c => TypedResults.Problem(
                Problem(StatusCodes.Status409Conflict, c.Code, c.Message)
            ),
            InvariantError i => TypedResults.Problem(
                Problem(StatusCodes.Status422UnprocessableEntity, i.Code, i.Message)
            ),
            ValidationError v => TypedResults.ValidationProblem(v.Errors),
            _ => throw new UnreachableException(),
        };

    private static ProblemDetails Problem(
        int status,
        string code,
        string detail,
        NotFoundError? notFound = null
    )
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Detail = detail,
            Extensions = { ["code"] = code },
        };

        if (notFound is null)
            return pd;

        pd.Extensions["entity"] = notFound.Entity;
        pd.Extensions["id"] = notFound.Id;

        return pd;
    }
}
