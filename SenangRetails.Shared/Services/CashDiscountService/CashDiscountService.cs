using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
using System.Collections.ObjectModel;

namespace SenangRetails.Shared.Services.CashDiscountService;

public class CashDiscountService : ICashDiscountService
{
    private readonly CashDiscountAC _ac;

    public CashDiscountService(CashDiscountAC ac)
    {
        _ac = ac;
    }

    public string? LastError => _ac.LastError;

    public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(CashDiscountDM objDiscount)
    {
        return await _ac.CreateAsync(objDiscount);
    }

    public async Task<ApiResponseRoot<object>?> Delete(string strID)
    {
        return await _ac.DeleteAsync(strID);
    }

    public async Task<ObservableCollection<CashDiscountDM>> LoadProxy()
    {
        return await _ac.LoadProxy();
    }

    public async Task<CashDiscountDM> LoadRecord(string strID)
    {
        return await _ac.LoadRecord(strID);
    }

    public async Task<ApiResponseRoot<string>?> UpdateRecord(CashDiscountDM objDiscount)
    {
        return await _ac.UpdateAsync(objDiscount);
    }
}
