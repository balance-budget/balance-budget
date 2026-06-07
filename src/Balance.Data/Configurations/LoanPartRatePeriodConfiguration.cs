using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class LoanPartRatePeriodConfiguration : IEntityTypeConfiguration<LoanPartRatePeriod>
{
    public void Configure(EntityTypeBuilder<LoanPartRatePeriod> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LoanPartRatePeriods");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasConversion<LoanPartRatePeriodId.EfCoreValueConverter>();

        builder
            .Property(r => r.LoanPartId)
            .HasConversion<LoanPartId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(r => r.EffectiveDate).IsRequired();

        builder.Property(r => r.AnnualRatePercent).HasPrecision(8, 4).IsRequired();

        builder.Property(r => r.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(r => r.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        // One rate per (part, effective date): "the rate in force" must be unambiguous.
        builder
            .HasIndex(r => new { r.LoanPartId, r.EffectiveDate })
            .IsUnique()
            .HasDatabaseName("IX_LoanPartRatePeriods_LoanPartId_EffectiveDate");
    }
}
