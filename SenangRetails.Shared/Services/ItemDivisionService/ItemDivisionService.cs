using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services.ItemDivisionService
{
    public class ItemDivisionService : IItemDivisionService
    {
        private const int SupportingTableTypeId = 53; // EnumSupportingTableType.ItemDivision
        private readonly ISupportingTableService _supportingTableService;

        public ItemDivisionService(ISupportingTableService supportingTableService)
        {
            _supportingTableService = supportingTableService;
        }

        private static ItemDivisionModel MapItem(SupportingTableItem x)
        {
            return new ItemDivisionModel
            {
                Id = x.SupportingTableID ?? string.Empty,
                Code = x.SupportingTableID ?? string.Empty,
                Name = x.SupportingTableName ?? string.Empty,
                Active = x.Active,
                Sequence = x.Sequence,
                CreatedDateTime = DateTime.TryParse(x.CreatedDateTime, out var dt) ? dt : null
            };
        }

        public async Task<List<ItemDivisionModel>> GetDivisionsAsync()
        {
            try
            {
                var apiItems = await _supportingTableService.LoadListByTypeAsync(SupportingTableTypeId);
                if (apiItems != null)
                {
                    return apiItems.Select(MapItem).OrderBy(x => x.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDivisionService] Failed to load divisions from API: {ex.Message}");
            }

            return new List<ItemDivisionModel>();
        }

        public async Task<(bool Success, string Message)> SaveDivisionAsync(ItemDivisionModel model)
        {
            bool isNew = string.IsNullOrWhiteSpace(model.Id);
            var divisionName = (model.Name ?? model.Description ?? string.Empty).Trim();
            var supportingModel = new SupportingTableModel
            {
                supportingTableID = isNew ? null : model.Id,
                supportingTableName = divisionName,
                description = null,
                textField = null,
                active = model.Active,
                supportingTableTypeID = SupportingTableTypeId,
                sequence = model.Sequence,
                saveAction = isNew ? "Added" : "Changed",
                isDirty = true
            };

            var (success, rawMessage) = isNew
                ? await _supportingTableService.CreateAsync(supportingModel)
                : await _supportingTableService.UpdateAsync(supportingModel);

            string userMessage = success
                ? (isNew ? "Division created successfully." : "Division updated successfully.")
                : (!string.IsNullOrWhiteSpace(rawMessage) ? rawMessage.Replace("category", "division").Replace("Category", "Division") : "Failed to save division.");

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

        public async Task<(bool Success, string Message)> DeleteDivisionAsync(string id)
        {
            var (success, rawMessage) = await _supportingTableService.DeleteAsync(id);
            string userMessage = success
                ? "Division deleted successfully."
                : (!string.IsNullOrWhiteSpace(rawMessage) ? rawMessage.Replace("category", "division").Replace("Category", "Division") : "Failed to delete division.");

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }
    }
}
