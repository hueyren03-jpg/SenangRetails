using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System.Collections.ObjectModel;

namespace SenangRetails.Shared.ApiClient;

public class CashDiscountAC : BaseAC
{
    private readonly IStoreTokenService _tokenService;

    public CashDiscountAC(IStoreTokenService tokenService) : base()
    {
        _tokenService = tokenService;
    }

    public string? LastError { get; private set; }

    private async Task<bool> SetBearerToken()
    {
        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            LastError = "Authentication token is unavailable.";
            return false;
        }

        LastError = null;
        return CreateBearerAuthAsync(token);
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateAsync(CashDiscountDM request)
    {
        if (!await SetBearerToken()) return null;

        var response = await PostAsync<CashDiscountDM, ApiResponseRoot<CreateResponse>>(
            "api/CashDiscount/CreateRecord",
            request);
        SetLastError(response);
        return response;
    }

    public async Task<ApiResponseRoot<string>?> UpdateAsync(CashDiscountDM request)
    {
        if (!await SetBearerToken()) return null;

        var response = await PutAsync<CashDiscountDM, ApiResponseRoot<string>>(
            "api/CashDiscount/UpdateRecord",
            request);
        SetLastError(response);
        return response;
    }

    public async Task<CashDiscountDM> LoadRecord(string strID)
    {
        if (!await SetBearerToken()) return new CashDiscountDM();

        var apiResponse = await PostAsync<object, ApiResponseRoot<CashDiscountDM>>(
            "api/CashDiscount/LoadRecord",
            new { Id = strID });
        SetLastError(apiResponse);
        return apiResponse?.result ?? new CashDiscountDM();
    }

    public async Task<ObservableCollection<CashDiscountDM>> LoadProxy()
    {
        if (!await SetBearerToken()) return new ObservableCollection<CashDiscountDM>();

        var apiResponse = await GetAsync<ApiResponseRoot<ObservableCollection<CashDiscountDM>>>(
            "api/CashDiscount/LoadProxy");
        SetLastError(apiResponse);
        return apiResponse?.result ?? new ObservableCollection<CashDiscountDM>();
    }

    public async Task<ApiResponseRoot<object>?> DeleteAsync(string strID)
    {
        if (!await SetBearerToken()) return null;
        var response = await DeleteAsync<ApiResponseRoot<object>>(
            $"api/CashDiscount/Delete?id={Uri.EscapeDataString(strID)}");
        SetLastError(response);
        return response;
    }

    private void SetLastError(IApiResponse? response)
    {
        LastError = response?.statusCode is >= 200 and < 300
            ? null
            : response?.message ?? "The CashDiscount API did not return a response.";
    }

}
