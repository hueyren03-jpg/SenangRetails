using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient;

public sealed class BarcodeFormatAC : BaseAC
{
    private readonly IStoreTokenService _tokenService;

    public BarcodeFormatAC(IStoreTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public string? LastError { get; private set; }

    private async Task<bool> SetBearerToken()
    {
        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            LastError = "Authentication token is unavailable.";
            return false;
        }

        LastError = null;
        return CreateBearerAuthAsync(token);
    }

    public async Task<ApiResponseRoot<List<BarcodeFormatApiModel>>?> LoadProxyAsync()
    {
        if (!await SetBearerToken()) return null;

        var response = await PostAsync<object, ApiResponseRoot<List<BarcodeFormatApiModel>>>(
            "api/BarcodeFormat/LoadProxy",
            new { });

        SetLastError(response);
        return response;
    }

    public async Task<ApiResponseRoot<BarcodeFormatApiModel>?> LoadRecordAsync(string id)
    {
        if (!await SetBearerToken()) return null;

        var response = await PostAsync<object, ApiResponseRoot<BarcodeFormatApiModel>>(
            "api/BarcodeFormat/LoadRecord",
            new { Id = id });

        SetLastError(response);
        return response;
    }

    public async Task<ApiResponseRoot<object>?> CreateRecordAsync(BarcodeFormatApiModel request)
    {
        if (!await SetBearerToken()) return null;

        var response = await PostAsync<BarcodeFormatApiModel, ApiResponseRoot<object>>(
            "api/BarcodeFormat/CreateRecord",
            request);

        SetLastError(response);
        return response;
    }

    public async Task<ApiResponseRoot<object>?> UpdateRecordAsync(BarcodeFormatApiModel request)
    {
        if (!await SetBearerToken()) return null;

        var response = await PutAsync<BarcodeFormatApiModel, ApiResponseRoot<object>>(
            "api/BarcodeFormat/UpdateRecord",
            request);

        SetLastError(response);
        return response;
    }

    public async Task<ApiResponseRoot<object>?> DeleteRecordAsync(string id)
    {
        if (!await SetBearerToken()) return null;

        var response = await DeleteAsync<ApiResponseRoot<object>>(
            $"api/BarcodeFormat/Delete?id={Uri.EscapeDataString(id)}");

        SetLastError(response);
        return response;
    }

    private void SetLastError(IApiResponse? response)
    {
        LastError = response?.statusCode is >= 200 and < 300
            ? null
            : response?.message ?? "The BarcodeFormat API did not return a response.";
    }
}
