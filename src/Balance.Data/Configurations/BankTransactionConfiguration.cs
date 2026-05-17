using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "BankTransactions",
            t =>
            {
                t.HasCheckConstraint("CK_BankTransactions_Amount_NonZero", "\"Amount\" <> 0");
            }
        );

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasConversion<BankTransactionId.EfCoreValueConverter>();

        builder
            .Property(b => b.BankAccountId)
            .HasConversion<BankAccountId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(b => b.BookingDate).IsRequired();

        builder.ComplexProperty(
            b => b.Money,
            money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").IsRequired();
                money
                    .Property(m => m.CurrencyCode)
                    .HasConversion<CurrencyCode.EfCoreValueConverter>()
                    .HasColumnName("CurrencyCode")
                    .HasMaxLength(8)
                    .IsRequired();
            }
        );

        builder.Property(b => b.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(b => b.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(b => b.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BankAccountId).HasDatabaseName("IX_BankTransactions_BankAccountId");

        builder.HasIndex(b => b.BookingDate).HasDatabaseName("IX_BankTransactions_BookingDate");
    }
}
