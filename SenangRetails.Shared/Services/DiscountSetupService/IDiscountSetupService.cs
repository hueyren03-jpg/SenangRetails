using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.DiscountSetupService
{
    public interface IDiscountSetupService
    {
        string? LastError { get; }
        Task<List<DiscountSetupModel>> GetDiscountsAsync();
        Task<DiscountSetupModel?> GetDiscountAsync(string id);
        Task<bool> SaveDiscountAsync(DiscountSetupModel discount, bool isNew);
        Task<bool> DeleteDiscountAsync(string id);
        Task<bool> ToggleActiveAsync(string id);
    }
}
