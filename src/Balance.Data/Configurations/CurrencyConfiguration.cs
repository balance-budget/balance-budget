using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Currencies");

        builder
            .Property(c => c.Code)
            .HasConversion<CurrencyCode.EfCoreValueConverter>()
            .HasMaxLength(8);

        builder.HasKey(c => c.Code);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Symbol).HasMaxLength(8);
        builder.Property(c => c.MinorUnitScale).IsRequired();

        builder.HasData(CurrencySeed.All);
    }
}
