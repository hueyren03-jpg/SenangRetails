using Microsoft.EntityFrameworkCore;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Database;

internal sealed class LocalAppDbContext(DbContextOptions<LocalAppDbContext> options) : DbContext(options)
{
    public DbSet<OfflineCashSaleEntity> OfflineCashSales => Set<OfflineCashSaleEntity>();
    public DbSet<LocalDataCacheEntity> LocalDataCaches => Set<LocalDataCacheEntity>();
    public DbSet<LocalCatalogEntity> LocalCatalogs => Set<LocalCatalogEntity>();
    public DbSet<LocalCustomerEntity> LocalCustomers => Set<LocalCustomerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalAppDbContext).Assembly);
    }
}
