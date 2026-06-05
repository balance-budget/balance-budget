using Balance.Data.Configurations;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balance.Data.Seeding;

/// <summary>
/// Drives the Development-only sample-data lifecycle behind EF Core's <c>UseAsyncSeeding</c> hook
/// (wired in <see cref="BalanceDbContext.OnConfiguring"/>, Development-guarded). On every startup it
/// wipes the previous sample ledger and rebuilds it from <see cref="DevelopmentSeedData"/> with dates
/// re-anchored to "today". Reference data (Currencies, the Opening Balances account) and Identity
/// users survive the wipe. See ADR-0021.
/// </summary>
internal static class DevelopmentDataSeeder
{
    public const string DeveloperEmail = "dev@balance.local";
    public const string DeveloperPassword = "developer-local-001";

    public static async Task SeedAsync(
        BalanceDbContext context,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        logger.DevelopmentSeedingStarted();

        await WipeAsync(context, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var graph = DevelopmentSeedData.Build(today);

        context.Accounts.AddRange(graph.Accounts);
        context.Counterparties.AddRange(graph.Counterparties);
        context.BankAccounts.AddRange(graph.BankAccounts);
        context.JournalEntries.AddRange(graph.JournalEntries);
        context.BankTransactions.AddRange(graph.BankTransactions);

        await EnsureDeveloperUserAsync(context, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.DevelopmentSeedingFinished(
            graph.Accounts.Count,
            graph.JournalEntries.Count,
            graph.BankTransactions.Count,
            DeveloperEmail,
            DeveloperPassword
        );
    }

    // Deletes the disposable sample ledger in FK-safe order. The HasData reference seeds
    // (Currencies, the Opening Balances account) and Identity users are deliberately excluded.
    private static async Task WipeAsync(
        BalanceDbContext context,
        CancellationToken cancellationToken
    )
    {
        await context.BankTransactionMetadataValues.ExecuteDeleteAsync(cancellationToken);
        await context.JournalLines.ExecuteDeleteAsync(cancellationToken);
        await context.BankTransactions.ExecuteDeleteAsync(cancellationToken);
        await context.JournalEntries.ExecuteDeleteAsync(cancellationToken);
        await context.BankAccounts.ExecuteDeleteAsync(cancellationToken);
        await context.Counterparties.ExecuteDeleteAsync(cancellationToken);

        // ParentAccountId is RESTRICT (ADR-0019), so break the self-references before bulk-deleting.
        await context
            .Accounts.Where(a => a.Id != AccountSeed.OpeningBalancesId && a.ParentAccountId != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.ParentAccountId, (AccountId?)null),
                cancellationToken
            );
        await context
            .Accounts.Where(a => a.Id != AccountSeed.OpeningBalancesId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Create-if-absent. Built by hand (the seeder holds only the DbContext, not UserManager),
    // mirroring what UserManager.CreateAsync would set. Never wiped on re-seed.
    private static async Task EnsureDeveloperUserAsync(
        BalanceDbContext context,
        CancellationToken cancellationToken
    )
    {
        var normalizedEmail = DeveloperEmail.ToUpperInvariant();
        if (
            await context.Users.AnyAsync(
                u => u.NormalizedEmail == normalizedEmail,
                cancellationToken
            )
        )
            return;

        var user = new BalanceUser
        {
            Id = new UserId(Guid.CreateVersion7()),
            UserName = DeveloperEmail,
            NormalizedUserName = normalizedEmail,
            Email = DeveloperEmail,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            DisplayName = "Developer",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        user.PasswordHash = new PasswordHasher<BalanceUser>().HashPassword(user, DeveloperPassword);
        context.Users.Add(user);
    }
}

/// <summary>The fully-built sample graph returned by <see cref="DevelopmentSeedData.Build"/>.</summary>
internal sealed class DevelopmentSeedGraph
{
    public required IReadOnlyList<Account> Accounts { get; init; }
    public required IReadOnlyList<Counterparty> Counterparties { get; init; }
    public required IReadOnlyList<BankAccount> BankAccounts { get; init; }
    public required IReadOnlyList<JournalEntry> JournalEntries { get; init; }
    public required IReadOnlyList<BankTransaction> BankTransactions { get; init; }
}
