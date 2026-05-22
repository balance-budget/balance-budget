using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Balance.Services.Helpers;

public static class QueryableExtensions
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
}
