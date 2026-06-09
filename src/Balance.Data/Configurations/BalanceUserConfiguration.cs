using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class BalanceUserConfiguration : IEntityTypeConfiguration<BalanceUser>
{
    public void Configure(EntityTypeBuilder<BalanceUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(u => u.Id).HasConversion<UserId.EfCoreValueConverter>();
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(u => u.Language).HasMaxLength(16);
        builder.Property(u => u.DateFormat).HasMaxLength(16);
        builder.Property(u => u.NumberFormat).HasMaxLength(16);
    }
}
