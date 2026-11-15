using Balance.Services.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Balance.Services.Helpers;

internal static class DbServiceErrorMapper
{
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintTrigger = 1811;
    private const int SqliteConstraintUnique = 2067;

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
        ex is DbUpdateException { InnerException: { } inner }
            ? MapProviderError(inner)
            : MapProviderError(ex);

    private static ConflictError? MapProviderError(Exception ex) =>
        ex switch
        {
            SqliteException
            {
                SqliteErrorCode: SqliteConstraint,
                SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey
            }
            or PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                new ConflictError(
                    ErrorCodes.UniquenessConflict,
                    "A conflicting record already exists."
                ),
            SqliteException
            {
                SqliteErrorCode: SqliteConstraint,
                SqliteExtendedErrorCode: SqliteConstraintForeignKey or SqliteConstraintTrigger
            }
            or PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                new ConflictError(
                    ErrorCodes.Referenced,
                    "This record is still referenced by other records."
                ),
            _ => null,
        };
}
