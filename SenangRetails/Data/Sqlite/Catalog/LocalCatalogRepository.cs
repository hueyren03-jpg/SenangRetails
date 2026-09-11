using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Data.Sqlite.Database;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Catalog;

internal sealed class LocalCatalogRepository(IDbContextFactory<LocalAppDbContext> contextFactory)
    : ILocalCatalogRepository
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<LocalCatalogSnapshot?> GetAsync(string branchId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var hasExplicitBranch = !string.IsNullOrWhiteSpace(branchId);
        var key = hasExplicitBranch ? branchId.Trim() : "default";
        var row = await db.LocalCatalogs.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == key, cancellationToken);
        if (row is null && !hasExplicitBranch)
            row = await db.LocalCatalogs.AsNoTracking()
                .OrderByDescending(x => x.LastUpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new LocalCatalogSnapshot(
                row.BranchId, row.ItemsJson, row.CategoriesJson,
                row.PaymentMethodsJson, row.LastUpdatedAtUtc);
    }

    public async Task UpsertAsync(
        string branchId,
        string? itemsJson = null,
        string? categoriesJson = null,
        string? paymentMethodsJson = null,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var key = string.IsNullOrWhiteSpace(branchId) ? "default" : branchId.Trim();
            await UpsertRowAsync(db, key, itemsJson, categoriesJson, paymentMethodsJson, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task UpsertRowAsync(
        LocalAppDbContext db,
        string key,
        string? itemsJson,
        string? categoriesJson,
        string? paymentMethodsJson,
        CancellationToken cancellationToken)
    {
        var row = await db.LocalCatalogs.FirstOrDefaultAsync(x => x.BranchId == key, cancellationToken);
        if (row is null)
        {
            row = new LocalCatalogEntity { BranchId = key };
            db.LocalCatalogs.Add(row);
        }
        if (itemsJson is not null) row.ItemsJson = itemsJson;
        if (categoriesJson is not null) row.CategoriesJson = categoriesJson;
        if (paymentMethodsJson is not null) row.PaymentMethodsJson = paymentMethodsJson;
        row.LastUpdatedAtUtc = DateTime.UtcNow;
    }
}
