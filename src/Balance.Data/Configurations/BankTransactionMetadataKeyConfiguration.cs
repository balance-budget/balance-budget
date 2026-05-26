using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class BankTransactionMetadataKeyConfiguration
    : IEntityTypeConfiguration<BankTransactionMetadataKey>
{
    public void Configure(EntityTypeBuilder<BankTransactionMetadataKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BankTransactionMetadataKeys");

        builder.HasKey(k => k.Id);

        builder
            .Property(k => k.Id)
            .HasConversion<BankTransactionMetadataKeyId.EfCoreValueConverter>();

        builder.Property(k => k.Name).IsRequired().HasMaxLength(128);

        builder
            .HasIndex(k => k.Name)
            .IsUnique()
            .HasDatabaseName("UX_BankTransactionMetadataKeys_Name");

        builder.Property(k => k.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(k => k.UpdatedAt).HasConversion(DateConverters.UtcConverter);
    }
}
