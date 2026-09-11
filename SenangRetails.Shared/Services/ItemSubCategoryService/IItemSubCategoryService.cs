using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ItemSubCategoryService
{
    public interface IItemSubCategoryService
    {
        Task<List<ItemSubCategoryModel>> GetSubCategoriesAsync();
        Task<(bool Success, string Message)> SaveSubCategoryAsync(ItemSubCategoryModel model);
        Task<(bool Success, string Message)> DeleteSubCategoryAsync(string id);
    }
}
