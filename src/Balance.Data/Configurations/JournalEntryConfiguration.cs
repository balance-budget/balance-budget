using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("JournalEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasConversion<JournalEntryId.EfCoreValueConverter>();

        builder.Property(e => e.Date).IsRequired();

        builder.Property(e => e.Description).HasMaxLength(512);

        builder
            .Property(e => e.CounterpartyId)
            .HasConversion<CounterpartyId.EfCoreValueConverter>();

        builder.Property(e => e.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(e => e.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasMany(e => e.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Counterparty>()
            .WithMany()
            .HasForeignKey(e => e.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Date).HasDatabaseName("IX_JournalEntries_Date");
        builder.HasIndex(e => e.CounterpartyId).HasDatabaseName("IX_JournalEntries_CounterpartyId");
    }
}
