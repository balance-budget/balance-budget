using Balance.Services.Contracts;
using EntityFramework.Exceptions.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Helpers;

internal static class DbServiceErrorMapper
{
    public static async Task<Result<T>> ExecuteAndCatch<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await action(cancellationToken);
            return new Result<T>(result);
        }
        catch (Exception ex) when (MapToError(ex) is { } error)
        {
            return error;
        }
    }

    public static async Task<Result> ExecuteAndCatch(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await action(cancellationToken);
            return Result.Success;
        }
        catch (Exception ex) when (MapToError(ex) is { } error)
        {
            return error;
        }
    }

    private static ConflictError? MapToError(Exception ex) =>
        ex switch
        {
            UniqueConstraintException => new ConflictError(
                ErrorCodes.UniquenessConflict,
                "A conflicting record already exists."
            ),
            ReferenceConstraintException
            or SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 767 }
            or SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 1811 }
            or DbUpdateException
            {
                InnerException: SqliteException
                    {
                        SqliteErrorCode: 19,
                        SqliteExtendedErrorCode: 767
                    }
                    or SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 1811 }
            } => new ConflictError(
                ErrorCodes.Referenced,
                "This record is still referenced by other records."
            ),
            _ => null,
        };
}
