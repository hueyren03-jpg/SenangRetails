using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ItemCategoryService
{
    public interface IItemCategoryService
    {
        Task<List<ItemCategoryModel>> GetCategoriesAsync();
        Task<(bool Success, string Message)> SaveCategoryAsync(ItemCategoryModel model);
        Task<(bool Success, string Message)> DeleteCategoryAsync(string id);
    }
}
