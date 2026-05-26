using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balance.Services.Logging;

public static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Error, "Failed to create {entity}")]
    public static partial void FailedToCreateEntity(
        this ILogger logger,
        DbUpdateException ex,
        string entity
    );

    [LoggerMessage(LogLevel.Error, "Failed to update {entity} {id}")]
    public static partial void FailedToUpdateEntity(
        this ILogger logger,
        DbUpdateException ex,
        string entity,
        string id
    );

    [LoggerMessage(LogLevel.Error, "Failed to delete {entity} {id}")]
    public static partial void FailedToDeleteEntity(
        this ILogger logger,
        DbUpdateException ex,
        string entity,
        string id
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Database optimization started at {DateTime}."
    )]
    public static partial void DatabaseOptimizationStarted(
        this ILogger logger,
        DateTimeOffset dateTime
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Database optimization finished at {DateTime}."
    )]
    public static partial void DatabaseOptimizationFinished(
        this ILogger logger,
        DateTimeOffset dateTime
    );
}
