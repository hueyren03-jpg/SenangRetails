using Microsoft.Extensions.DependencyInjection;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Services.DataLayer.Online;

namespace SenangRetails.Shared.Services.DataLayer;

public static class DataLayerServiceCollectionExtensions
{
    public static IServiceCollection AddSenangRetailsOnlineDataLayer(this IServiceCollection services)
    {
        services.AddSingleton<ILocalDataAvailability>(new LocalDataAvailability(false));
        services.AddSingleton<IOfflineCashSaleRepository, OnlineOnlyOfflineCashSaleRepository>();
        services.AddScoped<ILocalJsonCache, BrowserJsonCache>();
        services.AddSingleton<ILocalCatalogRepository, OnlineOnlyCatalogRepository>();
        services.AddSingleton<ILocalCustomerRepository, OnlineOnlyCustomerRepository>();
        return services;
    }
}
