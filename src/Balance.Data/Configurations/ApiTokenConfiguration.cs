using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ApiTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasConversion<ApiTokenId.EfCoreValueConverter>();

        builder.Property(t => t.UserId).HasConversion<UserId.EfCoreValueConverter>().IsRequired();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Prefix).IsRequired().HasMaxLength(16);
        builder.Property(t => t.Last4).IsRequired().HasMaxLength(4);

        builder.Property(t => t.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(t => t.UpdatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(t => t.LastUsedAt).HasConversion(DateConverters.UtcNullableConverter);
        builder.Property(t => t.ExpiresAt).HasConversion(DateConverters.UtcNullableConverter);
        builder.Property(t => t.RevokedAt).HasConversion(DateConverters.UtcNullableConverter);

        builder
            .HasOne<BalanceUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("IX_ApiTokens_TokenHash");
        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_ApiTokens_UserId");
    }
}
