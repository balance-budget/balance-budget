using Balance.Configuration.Options;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Balance.Data.Seeding;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balance.Data;

public sealed class BalanceDbContext
    : IdentityUserContext<BalanceUser, UserId>,
        IDataProtectionKeyContext
{
    private readonly IHostEnvironment _environment;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseOptions _options;
    private readonly TimeProvider _timeProvider;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<Currency> Currencies { get; set; } = null!;

    public DbSet<Account> Accounts { get; set; } = null!;

    public DbSet<Counterparty> Counterparties { get; set; } = null!;

    public DbSet<BankAccount> BankAccounts { get; set; } = null!;

    public DbSet<BankTransaction> BankTransactions { get; set; } = null!;

    public DbSet<BankTransactionMetadataKey> BankTransactionMetadataKeys { get; set; } = null!;

    public DbSet<BankTransactionMetadataValue> BankTransactionMetadataValues { get; set; } = null!;

    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;

    public DbSet<JournalLine> JournalLines { get; set; } = null!;

    public DbSet<ApiToken> ApiTokens { get; set; } = null!;

    public DatabaseProvider Provider => _options.Provider;

    public BalanceDbContext(
        IHostEnvironment environment,
        ILoggerFactory loggerFactory,
        IOptions<DatabaseOptions> options,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _environment = environment;
        _loggerFactory = loggerFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // EnableSensitiveDataLogging writes EF parameter values into the log stream — useful
        // when debugging queries locally, but it means anything the app reads or writes
        // ends up in logs. Never run a Development host against production-shaped data.
        optionsBuilder
            .UseProvider(_options)
            .UseLoggerFactory(_loggerFactory)
            .EnableDetailedErrors(_environment.IsDevelopment())
            .EnableSensitiveDataLogging(_environment.IsDevelopment());

        // Development-only sample data, rebuilt every startup with dates anchored to now (ADR-0024).
        // Reference data stays on HasData; this runtime hook is the only EF-suggested way to express
        // "Development-only" and "current-dated", neither of which HasData can do. Invoked by the
        // startup MigrateAsync; the guard also keeps it out of IntegrationTest and design-time hosts.
        if (_environment.IsDevelopment())
        {
            var logger = _loggerFactory.CreateLogger("Balance.Data.Seeding");
            optionsBuilder.UseAsyncSeeding(
                (context, _, cancellationToken) =>
                    DevelopmentDataSeeder.SeedAsync(
                        (BalanceDbContext)context,
                        _timeProvider,
                        logger,
                        cancellationToken
                    )
            );
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        // Apply the UserId converter globally so Identity's join tables
        // (IdentityUserClaim<UserId>, IdentityUserLogin<UserId>, IdentityUserToken<UserId>)
        // round-trip the typed key without per-entity wiring.
        configurationBuilder.Properties<UserId>().HaveConversion<UserId.EfCoreValueConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(BalanceDbContext).Assembly);
        builder.AddCaseInsensitiveLike(_options.Provider);
        builder.ApplyProviderConventions(_options.Provider);
    }
}
