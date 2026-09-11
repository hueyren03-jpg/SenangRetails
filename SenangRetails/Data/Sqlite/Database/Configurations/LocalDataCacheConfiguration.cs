using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Database.Configurations;

internal sealed class LocalDataCacheConfiguration : IEntityTypeConfiguration<LocalDataCacheEntity>
{
    public void Configure(EntityTypeBuilder<LocalDataCacheEntity> builder)
    {
        builder.ToTable("LocalDataCaches");
        builder.HasKey(x => x.CacheKey);
        builder.HasIndex(x => x.LastUpdatedAtUtc);
    }
}
