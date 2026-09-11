using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ItemDepartmentService
{
    public interface IItemDepartmentService
    {
        Task<List<ItemDepartmentModel>> GetDepartmentsAsync();
        Task<(bool Success, string Message)> SaveDepartmentAsync(ItemDepartmentModel model);
        Task<(bool Success, string Message)> DeleteDepartmentAsync(string id);
    }
}
