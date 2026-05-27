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
                t.HasCheckConstraint(
                    "CK_BankTransactions_Dismissed_Pair",
                    "(\"DismissedAt\" IS NULL AND \"DismissedReason\" IS NULL) "
                        + "OR (\"DismissedAt\" IS NOT NULL AND \"DismissedReason\" IS NOT NULL)"
                );
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

        builder.Property(b => b.Description).IsRequired().HasMaxLength(512);
        builder.Property(b => b.CounterpartyName).HasMaxLength(256);
        builder.Property(b => b.CounterpartyAccountNumber).HasMaxLength(64);
        builder.Property(b => b.RawSource).IsRequired();
        builder.Property(b => b.RowHash).IsRequired().IsFixedLength().HasMaxLength(64);

        builder.Property(b => b.Reference).HasMaxLength(256);
        builder.Property(b => b.MandateId).HasMaxLength(64);
        builder.Property(b => b.SepaCreditorId).HasMaxLength(64);
        builder.Property(b => b.ForeignCurrencyCode).HasMaxLength(8);
        builder.Property(b => b.ImporterKey).HasMaxLength(64);

        builder.Property(b => b.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(b => b.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder.Property(b => b.DismissedAt).HasConversion(DateConverters.UtcNullableConverter);
        builder.Property(b => b.DismissedReason).HasMaxLength(500);

        builder
            .Property(b => b.JournalEntryId)
            .HasConversion<JournalEntryId.EfCoreValueConverter>();

        builder
            .HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(b => b.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(b => b.JournalEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasMany(b => b.Metadata)
            .WithOne()
            .HasForeignKey(v => v.BankTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.BankAccountId).HasDatabaseName("IX_BankTransactions_BankAccountId");

        builder.HasIndex(b => b.BookingDate).HasDatabaseName("IX_BankTransactions_BookingDate");

        builder
            .HasIndex(b => b.JournalEntryId)
            .HasDatabaseName("IX_BankTransactions_JournalEntryId");

        builder
            .HasIndex(b => new { b.BankAccountId, b.RowHash })
            .IsUnique()
            .HasDatabaseName("UX_BankTransactions_BankAccountId_RowHash");
    }
}
