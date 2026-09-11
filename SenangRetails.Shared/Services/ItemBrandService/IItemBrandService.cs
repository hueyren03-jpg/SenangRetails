using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ItemBrandService
{
    public interface IItemBrandService
    {
        Task<List<ItemBrandModel>> GetBrandsAsync();
        Task<(bool Success, string Message)> SaveBrandAsync(ItemBrandModel model);
        Task<(bool Success, string Message)> DeleteBrandAsync(string id);
    }
}
