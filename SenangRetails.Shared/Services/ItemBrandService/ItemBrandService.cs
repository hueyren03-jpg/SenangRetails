using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services.ItemBrandService
{
    public class ItemBrandService : IItemBrandService
    {
        private const int SupportingTableTypeId = 34; // EnumSupportingTableType.ItemBrand
        private readonly ISupportingTableService _supportingTableService;

        public ItemBrandService(ISupportingTableService supportingTableService)
        {
            _supportingTableService = supportingTableService;
        }

        private static ItemBrandModel MapItem(SupportingTableItem x)
        {
            return new ItemBrandModel
            {
                Id = x.SupportingTableID ?? string.Empty,
                Code = x.SupportingTableID ?? string.Empty,
                Name = x.SupportingTableName ?? string.Empty,
                Active = x.Active,
                Sequence = x.Sequence,
                CreatedDateTime = DateTime.TryParse(x.CreatedDateTime, out var dt) ? dt : null
            };
        }

        public async Task<List<ItemBrandModel>> GetBrandsAsync()
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
                System.Diagnostics.Debug.WriteLine($"[ItemBrandService] Failed to load brands from API: {ex.Message}");
            }

            return new List<ItemBrandModel>();
        }

        public async Task<(bool Success, string Message)> SaveBrandAsync(ItemBrandModel model)
        {
            bool isNew = string.IsNullOrWhiteSpace(model.Id);
            var brandName = (model.Name ?? model.Description ?? string.Empty).Trim();
            var supportingModel = new SupportingTableModel
            {
                supportingTableID = isNew ? null : model.Id,
                supportingTableName = brandName,
                description = null,
                textField = null,
                parentID = null,
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
                ? (isNew ? $"Item brand '{brandName}' added successfully." : $"Item brand '{brandName}' updated successfully.")
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to save item brand." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

        public async Task<(bool Success, string Message)> DeleteBrandAsync(string id)
        {
            var (success, rawMessage) = await _supportingTableService.DeleteAsync(id);
            string userMessage = success
                ? "Item brand deleted successfully."
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to delete item brand." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

    }
}
