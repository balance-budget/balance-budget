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

        builder.Property(a => a.AccountType).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder
            .Property(a => a.CurrencyCode)
            .HasConversion<CurrencyCode.EfCoreValueConverter>()
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(a => a.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(a => a.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Currency>()
            .WithMany()
            .HasForeignKey(a => a.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Name).IsUnique().HasDatabaseName("IX_Accounts_Name");

        builder.HasData(AccountSeed.All);
    }
}
