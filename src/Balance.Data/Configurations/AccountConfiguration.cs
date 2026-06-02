using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasConversion<AccountId.EfCoreValueConverter>();

        builder.Property(a => a.Name).IsRequired().HasMaxLength(128);

        builder.Property(a => a.Code).IsRequired().HasMaxLength(32);

        builder.Property(a => a.AccountType).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder
            .Property(a => a.CurrencyCode)
            .HasConversion<CurrencyCode.EfCoreValueConverter>()
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(a => a.IsPostable).IsRequired();

        builder.Property(a => a.ParentAccountId).HasConversion<AccountId.EfCoreValueConverter>();

        builder.Property(a => a.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(a => a.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Currency>()
            .WithMany()
            .HasForeignKey(a => a.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referential chart-of-accounts tree (ADR-0022). RESTRICT so a parent with children
        // cannot be deleted until they are re-parented or removed.
        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Code is the globally-unique human key. Name is no longer unique (ADR-0022).
        builder.HasIndex(a => a.Code).IsUnique().HasDatabaseName("IX_Accounts_Code");

        builder.HasIndex(a => a.ParentAccountId).HasDatabaseName("IX_Accounts_ParentAccountId");

        builder.HasData(AccountSeed.All);
    }
}
