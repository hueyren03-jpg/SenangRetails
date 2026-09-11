using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.DataLayer.Online;

internal sealed class OnlineOnlyCatalogRepository : ILocalCatalogRepository
{
    public Task<LocalCatalogSnapshot?> GetAsync(string branchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<LocalCatalogSnapshot?>(null);

    public Task UpsertAsync(string branchId, string? itemsJson = null, string? categoriesJson = null, string? paymentMethodsJson = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class OnlineOnlyCustomerRepository : ILocalCustomerRepository
{
    public Task<IReadOnlyList<LocalCustomerRecord>> SearchAsync(string keyword, int limit = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LocalCustomerRecord>>([]);

    public Task<LocalCustomerRecord?> GetAsync(string idOrPhone, CancellationToken cancellationToken = default) =>
        Task.FromResult<LocalCustomerRecord?>(null);

    public Task UpsertAsync(IEnumerable<LocalCustomerRecord> customers, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
