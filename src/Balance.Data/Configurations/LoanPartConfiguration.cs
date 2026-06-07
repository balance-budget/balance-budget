using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class LoanPartConfiguration : IEntityTypeConfiguration<LoanPart>
{
    public void Configure(EntityTypeBuilder<LoanPart> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LoanParts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasConversion<LoanPartId.EfCoreValueConverter>();

        builder.Property(p => p.LoanId).HasConversion<LoanId.EfCoreValueConverter>().IsRequired();

        builder.Property(p => p.Label).IsRequired().HasMaxLength(64);

        builder
            .Property(p => p.RepaymentType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.StartDate).IsRequired();
        builder.Property(p => p.EndDate).IsRequired();

        builder
            .Property(p => p.AccountId)
            .HasConversion<AccountId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(p => p.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(p => p.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.RatePeriods).WithOne().HasForeignKey(r => r.LoanPartId);

        // One part per account — loan-managed status is derived from this linkage.
        builder.HasIndex(p => p.AccountId).IsUnique().HasDatabaseName("IX_LoanParts_AccountId");
        builder.HasIndex(p => p.LoanId).HasDatabaseName("IX_LoanParts_LoanId");
    }
}
