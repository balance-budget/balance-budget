using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "BankAccounts",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_BankAccounts_OwnershipXor",
                    "(\"AccountId\" IS NULL) <> (\"CounterpartyId\" IS NULL)"
                );
                t.HasCheckConstraint(
                    "CK_BankAccounts_IbanOrAccountNumber",
                    "\"Iban\" IS NOT NULL OR \"AccountNumber\" IS NOT NULL"
                );
            }
        );

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasConversion<BankAccountId.EfCoreValueConverter>();

        builder.Property(b => b.Iban).HasMaxLength(34);
        builder.Property(b => b.AccountNumber).HasMaxLength(64);
        builder.Property(b => b.Bic).HasMaxLength(11);
        builder.Property(b => b.BankName).HasMaxLength(128);
        builder.Property(b => b.AccountHolderName).HasMaxLength(128);

        builder
            .Property(b => b.CurrencyCode)
            .HasConversion<CurrencyCode.EfCoreValueConverter>()
            .HasMaxLength(8);

        builder.Property(b => b.AccountId).HasConversion<AccountId.EfCoreValueConverter>();
        builder
            .Property(b => b.CounterpartyId)
            .HasConversion<CounterpartyId.EfCoreValueConverter>();

        builder.Property(b => b.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(b => b.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Currency>()
            .WithMany()
            .HasForeignKey(b => b.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Counterparty>()
            .WithMany()
            .HasForeignKey(b => b.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(b => b.Iban)
            .IsUnique()
            .HasFilter("\"Iban\" IS NOT NULL")
            .HasDatabaseName("IX_BankAccounts_Iban");

        builder
            .HasIndex(b => b.AccountId)
            .IsUnique()
            .HasFilter("\"AccountId\" IS NOT NULL")
            .HasDatabaseName("IX_BankAccounts_AccountId");

        builder.HasIndex(b => b.CounterpartyId).HasDatabaseName("IX_BankAccounts_CounterpartyId");
    }
}
