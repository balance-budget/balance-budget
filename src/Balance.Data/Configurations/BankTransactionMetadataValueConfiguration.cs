using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class BankTransactionMetadataValueConfiguration
    : IEntityTypeConfiguration<BankTransactionMetadataValue>
{
    public void Configure(EntityTypeBuilder<BankTransactionMetadataValue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "BankTransactionMetadataValues",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_BankTransactionMetadataValues_Value_Exactly_One",
                    "(\"StringValue\" IS NULL) <> (\"IntegerValue\" IS NULL)"
                );
            }
        );

        builder.HasKey(v => new { v.BankTransactionId, v.KeyId });

        builder
            .Property(v => v.BankTransactionId)
            .HasConversion<BankTransactionId.EfCoreValueConverter>()
            .IsRequired();

        builder
            .Property(v => v.KeyId)
            .HasConversion<BankTransactionMetadataKeyId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(v => v.StringValue);
        builder.Property(v => v.IntegerValue);

        builder
            .HasOne(v => v.Key)
            .WithMany()
            .HasForeignKey(v => v.KeyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.KeyId).HasDatabaseName("IX_BankTransactionMetadataValues_KeyId");
    }
}
