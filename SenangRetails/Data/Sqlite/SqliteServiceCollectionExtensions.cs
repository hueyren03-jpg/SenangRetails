using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Data.Sqlite.Catalog;
using SenangRetails.Data.Sqlite.Customers;
using SenangRetails.Data.Sqlite.Database;
using SenangRetails.Data.Sqlite.ReferenceData;
using SenangRetails.Data.Sqlite.Sales;

namespace SenangRetails.Data.Sqlite;

public static class SqliteServiceCollectionExtensions
{
    public static IServiceCollection AddSenangRetailsSqliteData(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();

        services.AddDbContextFactory<LocalAppDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddSingleton<ILocalDataAvailability>(new LocalDataAvailability(true));
        services.AddSingleton<ILocalDatabaseInitializer, LocalDatabaseInitializer>();
        services.AddSingleton<IOfflineCashSaleRepository, OfflineCashSaleRepository>();
        services.AddSingleton<ILocalJsonCache, LocalJsonCache>();
        services.AddSingleton<ILocalCatalogRepository, LocalCatalogRepository>();
        services.AddSingleton<ILocalCustomerRepository, LocalCustomerRepository>();
        return services;
    }
}
