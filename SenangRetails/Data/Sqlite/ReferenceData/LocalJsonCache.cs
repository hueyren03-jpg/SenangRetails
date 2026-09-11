using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Data.Sqlite.Database;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.ReferenceData;

internal sealed class LocalJsonCache(IDbContextFactory<LocalAppDbContext> contextFactory) : ILocalJsonCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.LocalDataCaches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CacheKey == key, cancellationToken);

        return string.IsNullOrWhiteSpace(row?.DataJson)
            ? default
            : JsonSerializer.Deserialize<T>(row.DataJson, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.LocalDataCaches.FirstOrDefaultAsync(x => x.CacheKey == key, cancellationToken);
        if (row is null)
        {
            row = new LocalDataCacheEntity { CacheKey = key };
            db.LocalDataCaches.Add(row);
        }

        row.DataJson = JsonSerializer.Serialize(value, JsonOptions);
        row.LastUpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
