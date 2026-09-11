namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public sealed record LocalCatalogSnapshot(
    string BranchId,
    string ItemsJson,
    string CategoriesJson,
    string PaymentMethodsJson,
    DateTime LastUpdatedAtUtc);

public interface ILocalCatalogRepository
{
    Task<LocalCatalogSnapshot?> GetAsync(string branchId, CancellationToken cancellationToken = default);
    Task UpsertAsync(
        string branchId,
        string? itemsJson = null,
        string? categoriesJson = null,
        string? paymentMethodsJson = null,
        CancellationToken cancellationToken = default);
}
