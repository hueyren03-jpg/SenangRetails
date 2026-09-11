namespace SenangRetails.Data.Sqlite.Database.Entities;

internal sealed class LocalDataCacheEntity
{
    public string CacheKey { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; }
}
