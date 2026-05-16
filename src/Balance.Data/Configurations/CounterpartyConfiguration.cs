using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class CounterpartyConfiguration : IEntityTypeConfiguration<Counterparty>
{
    public void Configure(EntityTypeBuilder<Counterparty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Counterparties");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasConversion<CounterpartyId.EfCoreValueConverter>();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);

        builder.Property(c => c.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(c => c.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("IX_Counterparties_Name");
    }
}
