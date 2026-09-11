using System.Text.Json;
using Microsoft.JSInterop;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.DataLayer.Online;

internal sealed class BrowserJsonCache(IJSRuntime jsRuntime) : ILocalJsonCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            key);

        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            cancellationToken,
            key,
            json);
    }
}
