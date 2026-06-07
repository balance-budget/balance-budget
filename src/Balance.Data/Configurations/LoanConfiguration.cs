using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balance.Data.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Loans");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasConversion<LoanId.EfCoreValueConverter>();

        builder.Property(l => l.Name).IsRequired().HasMaxLength(128);

        builder
            .Property(l => l.LenderCounterpartyId)
            .HasConversion<CounterpartyId.EfCoreValueConverter>()
            .IsRequired();

        builder
            .Property(l => l.InterestExpenseAccountId)
            .HasConversion<AccountId.EfCoreValueConverter>()
            .IsRequired();

        builder
            .Property(l => l.ParentAccountId)
            .HasConversion<AccountId.EfCoreValueConverter>()
            .IsRequired();

        builder.Property(l => l.CreatedAt).HasConversion(DateConverters.UtcConverter);
        builder.Property(l => l.UpdatedAt).HasConversion(DateConverters.UtcConverter);

        builder
            .HasOne<Counterparty>()
            .WithMany()
            .HasForeignKey(l => l.LenderCounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(l => l.InterestExpenseAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(l => l.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Parts).WithOne().HasForeignKey(p => p.LoanId);

        // One loan per parent account — the parent account *is* the loan in ledger terms.
        builder
            .HasIndex(l => l.ParentAccountId)
            .IsUnique()
            .HasDatabaseName("IX_Loans_ParentAccountId");
        builder
            .HasIndex(l => l.LenderCounterpartyId)
            .HasDatabaseName("IX_Loans_LenderCounterpartyId");
        builder
            .HasIndex(l => l.InterestExpenseAccountId)
            .HasDatabaseName("IX_Loans_InterestExpenseAccountId");
    }
}
