using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.ItemCategoryService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services.ItemSubCategoryService
{
    public class ItemSubCategoryService : IItemSubCategoryService
    {
        private const int SupportingTableTypeId = 56; // EnumSupportingTableType.ItemSubCategory
        private readonly ISupportingTableService _supportingTableService;
        private readonly IItemCategoryService _categoryService;

        public ItemSubCategoryService(
            ISupportingTableService supportingTableService,
            IItemCategoryService categoryService)
        {
            _supportingTableService = supportingTableService;
            _categoryService = categoryService;
        }

        private static ItemSubCategoryModel MapItem(SupportingTableItem x, Dictionary<string, string> categoryMap)
        {
            var catId = x.ParentID ?? string.Empty;
            categoryMap.TryGetValue(catId, out var catName);

            return new ItemSubCategoryModel
            {
                Id = x.SupportingTableID ?? string.Empty,
                Code = x.SupportingTableID ?? string.Empty,
                Name = x.SupportingTableName ?? string.Empty,
                CategoryId = catId,
                CategoryName = catName ?? string.Empty,
                Active = x.Active,
                Sequence = x.Sequence,
                CreatedDateTime = DateTime.TryParse(x.CreatedDateTime, out var dt) ? dt : null
            };
        }

        public async Task<List<ItemSubCategoryModel>> GetSubCategoriesAsync()
        {
            try
            {
                var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var categories = await _categoryService.GetCategoriesAsync();
                    foreach (var c in categories)
                    {
                        if (!string.IsNullOrEmpty(c.Id))
                        {
                            categoryMap[c.Id] = !string.IsNullOrWhiteSpace(c.Name) ? c.Name : c.Code;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ItemSubCategoryService] Failed to load categories for mapping: {ex.Message}");
                }

                var apiItems = await _supportingTableService.LoadListByTypeAsync(SupportingTableTypeId);
                if (apiItems != null)
                {
                    return apiItems.Select(x => MapItem(x, categoryMap)).OrderBy(x => x.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemSubCategoryService] Failed to load sub categories from API: {ex.Message}");
            }

            return new List<ItemSubCategoryModel>();
        }

        public async Task<(bool Success, string Message)> SaveSubCategoryAsync(ItemSubCategoryModel model)
        {
            bool isNew = string.IsNullOrWhiteSpace(model.Id);
            var subName = (model.Name ?? model.Description ?? string.Empty).Trim();
            var supportingModel = new SupportingTableModel
            {
                supportingTableID = isNew ? null : model.Id,
                supportingTableName = subName,
                description = null,
                textField = null,
                parentID = string.IsNullOrWhiteSpace(model.CategoryId) ? null : model.CategoryId.Trim(),
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
                ? (isNew ? $"Sub category '{subName}' added successfully." : $"Sub category '{subName}' updated successfully.")
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to save item sub category." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

        public async Task<(bool Success, string Message)> DeleteSubCategoryAsync(string id)
        {
            var (success, rawMessage) = await _supportingTableService.DeleteAsync(id);
            string userMessage = success
                ? "Sub category deleted successfully."
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to delete item sub category." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

    }
}
