using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ItemDivisionService
{
    public interface IItemDivisionService
    {
        Task<List<ItemDivisionModel>> GetDivisionsAsync();
        Task<(bool Success, string Message)> SaveDivisionAsync(ItemDivisionModel model);
        Task<(bool Success, string Message)> DeleteDivisionAsync(string id);
    }
}
