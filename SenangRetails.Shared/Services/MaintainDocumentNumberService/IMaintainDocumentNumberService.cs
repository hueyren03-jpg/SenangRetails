using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.MaintainDocumentNumberService
{
    public interface IMaintainDocumentNumberService
    {
        Task<List<MaintainDocumentNumberModel>> GetDocumentNumbersAsync(string? category = null);
        Task<bool> SaveDocumentNumbersAsync(List<MaintainDocumentNumberModel> list);
        Task<string?> ReserveNextNumberAsync(string category, int docType, string? name = null);
    }
}
