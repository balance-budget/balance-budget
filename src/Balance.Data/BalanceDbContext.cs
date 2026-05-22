using Balance.Configuration.Options;
using Balance.Data.Entities;
using Balance.Data.Helpers;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balance.Data;

public sealed class BalanceDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IHostEnvironment _environment;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseOptions _options;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<Currency> Currencies { get; set; } = null!;

    public DbSet<Account> Accounts { get; set; } = null!;

    public DbSet<Counterparty> Counterparties { get; set; } = null!;

    public DbSet<BankAccount> BankAccounts { get; set; } = null!;

    public DbSet<BankTransaction> BankTransactions { get; set; } = null!;

    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;

    public DbSet<JournalLine> JournalLines { get; set; } = null!;

    public DatabaseProvider Provider => _options.Provider;

    public BalanceDbContext(
        IHostEnvironment environment,
        ILoggerFactory loggerFactory,
        IOptions<DatabaseOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _environment = environment;
        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder
            .UseProvider(_options)
            .UseLoggerFactory(_loggerFactory)
            .EnableDetailedErrors(_environment.IsDevelopment())
            .EnableSensitiveDataLogging(_environment.IsDevelopment());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BalanceDbContext).Assembly);

        // Case-insensitive Name columns on Account and Counterparty. Both equality and
        // pattern matching (LIKE / Contains / StartsWith) must behave the same on both
        // providers — anticipating typeahead search on these columns. NOCASE collation on
        // SQLite and the citext extension on Postgres both deliver case-insensitive =, LIKE,
        // and ORDER BY without any per-query escape hatches.
        if (_options.Provider == DatabaseProvider.Sqlite)
        {
            modelBuilder.Entity<Account>().Property(a => a.Name).UseCollation("NOCASE");
            modelBuilder.Entity<Counterparty>().Property(c => c.Name).UseCollation("NOCASE");
        }
        else if (_options.Provider == DatabaseProvider.Postgres)
        {
            modelBuilder.HasPostgresExtension("citext");
            modelBuilder.Entity<Account>().Property(a => a.Name).HasColumnType("citext");
            modelBuilder.Entity<Counterparty>().Property(c => c.Name).HasColumnType("citext");
        }
    }
}
