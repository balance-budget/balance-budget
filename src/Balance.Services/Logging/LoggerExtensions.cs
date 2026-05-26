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

    // Throwaway log messages for the one-shot re-extraction backfill (issue #89);
    // removed in the follow-up PR alongside the backfill itself.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "BankTransaction re-extraction backfill started."
    )]
    public static partial void BankTransactionReExtractionBackfillStarted(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "BankTransaction re-extraction backfill finished: "
            + "{Visited} visited, {Updated} updated, "
            + "{SkippedNoImporterKey} skipped (no ImporterKey), "
            + "{SkippedNoExtractor} skipped (no extractor for ImporterKey), "
            + "{SkippedExtractorError} skipped (extractor error)."
    )]
    public static partial void BankTransactionReExtractionBackfillFinished(
        this ILogger logger,
        int visited,
        int updated,
        int skippedNoImporterKey,
        int skippedNoExtractor,
        int skippedExtractorError
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "BankTransaction re-extraction backfill skipping {BankTransactionId}: "
            + "no extractor registered for ImporterKey {ImporterKey}."
    )]
    public static partial void BankTransactionReExtractionMissingExtractor(
        this ILogger logger,
        Guid bankTransactionId,
        string importerKey
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "BankTransaction re-extraction backfill skipping {BankTransactionId} "
            + "(ImporterKey {ImporterKey}): extractor returned {ErrorCode} {ErrorMessage}."
    )]
    public static partial void BankTransactionReExtractionExtractorError(
        this ILogger logger,
        Guid bankTransactionId,
        string importerKey,
        string errorCode,
        string errorMessage
    );
}
