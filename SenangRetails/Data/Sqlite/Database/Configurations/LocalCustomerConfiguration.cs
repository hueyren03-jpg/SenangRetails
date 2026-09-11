using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Database.Configurations;

internal sealed class LocalCustomerConfiguration : IEntityTypeConfiguration<LocalCustomerEntity>
{
    public void Configure(EntityTypeBuilder<LocalCustomerEntity> builder)
    {
        builder.ToTable("LocalCustomers");
        builder.HasKey(x => x.MasterAccountId);
        builder.Property(x => x.MasterAccountId).HasMaxLength(128);
        builder.Property(x => x.AccountName).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(64);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Nric).HasColumnName("NRIC").HasMaxLength(64);
        builder.Property(x => x.MembershipTypeName).HasMaxLength(128);
        builder.HasIndex(x => x.AccountName);
        builder.HasIndex(x => x.Phone);
        builder.HasIndex(x => x.Nric).HasDatabaseName("IX_LocalCustomers_NRIC");
        builder.HasIndex(x => x.Email);
    }
}
