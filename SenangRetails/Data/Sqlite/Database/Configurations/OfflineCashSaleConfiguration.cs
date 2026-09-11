using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Database.Configurations;

internal sealed class OfflineCashSaleConfiguration : IEntityTypeConfiguration<OfflineCashSaleEntity>
{
    public void Configure(EntityTypeBuilder<OfflineCashSaleEntity> builder)
    {
        builder.ToTable("OfflineCashSales");
        builder.HasKey(x => x.LocalId);
        builder.Property(x => x.LocalId).HasMaxLength(64);
        builder.Property(x => x.LocalDisplayCode).IsRequired().HasMaxLength(64);
        builder.Property(x => x.BranchId).HasMaxLength(64);
        builder.Property(x => x.AccountId).HasMaxLength(64);
        builder.Property(x => x.AccountName).HasMaxLength(128);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.OrderPayloadJson).IsRequired();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.FinancialDate);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.NextAttemptAtUtc);
    }
}
