using Microsoft.Extensions.Logging;

namespace Balance.Data.Logging;

internal static partial class LoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration started.")]
    public static partial void DatabaseMigrationStarted(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration finished.")]
    public static partial void DatabaseMigrationFinished(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Database schema ensured from model (integration test)."
    )]
    public static partial void DatabaseSchemaEnsured(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Development data seeding started.")]
    public static partial void DevelopmentSeedingStarted(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Development data seeded: {Accounts} accounts, {JournalEntries} journal entries, {BankTransactions} bank transactions. Sign in with {Email} / {Password}."
    )]
    public static partial void DevelopmentSeedingFinished(
        this ILogger logger,
        int accounts,
        int journalEntries,
        int bankTransactions,
        string email,
        string password
    );
}
