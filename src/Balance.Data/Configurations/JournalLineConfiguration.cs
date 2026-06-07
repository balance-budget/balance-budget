using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "JournalLines",
            t =>
            {
                t.HasCheckConstraint("CK_JournalLines_Amount_NonZero", "\"Amount\" <> 0");
            }
        );

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasConversion<JournalLineId.EfCoreValueConverter>();

        builder
            .Property(l => l.JournalEntryId)
            .HasConversion<JournalEntryId.EfCoreValueConverter>()
            .IsRequired();

        builder
            .Property(l => l.AccountId)
            .HasConversion<AccountId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(l => l.Amount).IsRequired();

        builder
            .Property(l => l.ReconciliationStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(l => l.Description).HasMaxLength(512);

        builder.Property(l => l.LoanPartId).HasConversion<LoanPartId.EfCoreValueConverter>();

        builder.Property(l => l.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(l => l.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Attribution is a tag, not ownership: deleting a LoanPart strips the tag and the
        // posted history stands on its own (ADR-0025: the ledger is the source of truth).
        builder
            .HasOne<LoanPart>()
            .WithMany()
            .HasForeignKey(l => l.LoanPartId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.JournalEntryId).HasDatabaseName("IX_JournalLines_JournalEntryId");
        builder.HasIndex(l => l.AccountId).HasDatabaseName("IX_JournalLines_AccountId");
        builder.HasIndex(l => l.LoanPartId).HasDatabaseName("IX_JournalLines_LoanPartId");
    }
}
