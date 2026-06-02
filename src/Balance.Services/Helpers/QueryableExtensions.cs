using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Balance.Services.Helpers;

internal static class QueryableExtensions
{
    public static Task<Result<int>> ExecuteDeleteAndCatchAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return DbServiceErrorMapper.ExecuteAndCatch(source.ExecuteDeleteAsync, cancellationToken);
    }

    public static Task<Result<int>> ExecuteUpdateAndCatchAsync<TSource>(
        this IQueryable<TSource> source,
        Action<UpdateSettersBuilder<TSource>> setPropertyCalls,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return DbServiceErrorMapper.ExecuteAndCatch(
            ct => source.ExecuteUpdateAsync(setPropertyCalls, ct),
            cancellationToken
        );
    }

    /// <summary>
    /// Deletes the rows matched by <paramref name="source"/> (expected to be filtered to a single
    /// entity by id) and maps the outcome: DB faults to a <see cref="ConflictError"/>, "nothing
    /// deleted" to a <see cref="NotFoundError"/>, otherwise success.
    /// </summary>
    public static async Task<Result> DeleteSingleAndCatchAsync<TSource>(
        this IQueryable<TSource> source,
        string entityName,
        string id,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = await source.ExecuteDeleteAndCatchAsync(cancellationToken);
        if (result.IsFailure)
            return result.Error;
        if (result.Value == 0)
            return new NotFoundError(entityName, id);
        return Result.Success;
    }

    /// <summary>
    /// Returns success when <paramref name="matching"/> (a query filtered to a referenced entity)
    /// has any row, otherwise a <see cref="NotFoundError"/> — the "does this FK target exist?" guard.
    /// </summary>
    public static async Task<Result> EnsureExistsAsync<TSource>(
        this IQueryable<TSource> matching,
        string entityName,
        string id,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(matching);
        if (!await matching.AnyAsync(cancellationToken))
            return new NotFoundError(entityName, id);
        return Result.Success;
    }

    /// <summary>
    /// Returns success when <paramref name="conflicting"/> (a query filtered to rows that would
    /// collide, e.g. same name excluding self) has no row, otherwise a <see cref="ConflictError"/>
    /// — the uniqueness guard.
    /// </summary>
    public static async Task<Result> EnsureNoneAsync<TSource>(
        this IQueryable<TSource> conflicting,
        string errorCode,
        string message,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(conflicting);
        if (await conflicting.AnyAsync(cancellationToken))
            return new ConflictError(errorCode, message);
        return Result.Success;
    }
}
