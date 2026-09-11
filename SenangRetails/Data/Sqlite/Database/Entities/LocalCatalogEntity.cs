namespace SenangRetails.Data.Sqlite.Database.Entities;

internal sealed class LocalCatalogEntity
{
    public string BranchId { get; set; } = "default";
    public string ItemsJson { get; set; } = "[]";
    public string CategoriesJson { get; set; } = "[]";
    public string PaymentMethodsJson { get; set; } = "[]";
    public DateTime LastUpdatedAtUtc { get; set; }
}
