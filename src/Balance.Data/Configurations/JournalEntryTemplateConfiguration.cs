using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class JournalEntryTemplateConfiguration
    : IEntityTypeConfiguration<JournalEntryTemplate>
{
    public void Configure(EntityTypeBuilder<JournalEntryTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("JournalEntryTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasConversion<JournalEntryTemplateId.EfCoreValueConverter>();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);

        builder
            .Property(t => t.AccountId)
            .HasConversion<AccountId.EfCoreValueConverter>()
            .IsRequired();
        builder.Property(t => t.CounterAccountId).HasConversion<AccountId.EfCoreValueConverter>();
        builder
            .Property(t => t.CounterpartyId)
            .HasConversion<CounterpartyId.EfCoreValueConverter>();

        builder.Property(t => t.Cadence).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.AnchorDate).IsRequired();
        builder.Property(t => t.ExpectedAmount).IsRequired();

        builder.Property(t => t.MandateId).HasMaxLength(64);
        builder.Property(t => t.SepaCreditorId).HasMaxLength(64);

        builder.Property(t => t.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(t => t.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        // The pinned bank-side account and the optional counter/counterparty are referenced, never
        // owned — Restrict so a template can never silently disappear with an account deletion.
        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.CounterAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Counterparty>()
            .WithMany()
            .HasForeignKey(t => t.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.AccountId).HasDatabaseName("IX_JournalEntryTemplates_AccountId");
    }
}
