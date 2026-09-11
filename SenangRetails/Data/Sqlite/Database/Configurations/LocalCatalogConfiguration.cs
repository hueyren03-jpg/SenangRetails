using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Database.Configurations;

internal sealed class LocalCatalogConfiguration : IEntityTypeConfiguration<LocalCatalogEntity>
{
    public void Configure(EntityTypeBuilder<LocalCatalogEntity> builder)
    {
        builder.ToTable("LocalItemCatalogs");
        builder.HasKey(x => x.BranchId);
        builder.Property(x => x.BranchId).HasMaxLength(64);
    }
}
