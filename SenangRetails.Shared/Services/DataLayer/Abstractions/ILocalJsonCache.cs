namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public interface ILocalJsonCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
