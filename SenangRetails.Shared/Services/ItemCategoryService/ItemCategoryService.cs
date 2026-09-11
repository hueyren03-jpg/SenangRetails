using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.ItemDepartmentService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services.ItemCategoryService
{
    public class ItemCategoryService : IItemCategoryService
    {
        private const int SupportingTableTypeId = 55; // EnumSupportingTableType.ItemCategory
        private readonly ISupportingTableService _supportingTableService;
        private readonly IItemDepartmentService _departmentService;

        public ItemCategoryService(
            ISupportingTableService supportingTableService,
            IItemDepartmentService departmentService)
        {
            _supportingTableService = supportingTableService;
            _departmentService = departmentService;
        }

        private static ItemCategoryModel MapItem(SupportingTableItem x, Dictionary<string, string> departmentMap)
        {
            var deptId = x.ParentID ?? string.Empty;
            departmentMap.TryGetValue(deptId, out var deptName);

            return new ItemCategoryModel
            {
                Id = x.SupportingTableID ?? string.Empty,
                Code = x.SupportingTableID ?? string.Empty,
                Name = x.SupportingTableName ?? string.Empty,
                DepartmentId = deptId,
                DepartmentName = deptName ?? string.Empty,
                Active = x.Active,
                Sequence = x.Sequence,
                CreatedDateTime = DateTime.TryParse(x.CreatedDateTime, out var dt) ? dt : null
            };
        }

        public async Task<List<ItemCategoryModel>> GetCategoriesAsync()
        {
            try
            {
                var departmentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var departments = await _departmentService.GetDepartmentsAsync();
                    foreach (var d in departments)
                    {
                        if (!string.IsNullOrEmpty(d.Id))
                        {
                            departmentMap[d.Id] = !string.IsNullOrWhiteSpace(d.Name) ? d.Name : d.Code;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ItemCategoryService] Failed to load departments for mapping: {ex.Message}");
                }

                var apiItems = await _supportingTableService.LoadListByTypeAsync(SupportingTableTypeId);
                if (apiItems != null)
                {
                    return apiItems.Select(x => MapItem(x, departmentMap)).OrderBy(x => x.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemCategoryService] Failed to load categories from API: {ex.Message}");
            }

            return new List<ItemCategoryModel>();
        }

        public async Task<(bool Success, string Message)> SaveCategoryAsync(ItemCategoryModel model)
        {
            bool isNew = string.IsNullOrWhiteSpace(model.Id);
            var catName = (model.Name ?? model.Description ?? string.Empty).Trim();
            var supportingModel = new SupportingTableModel
            {
                supportingTableID = isNew ? null : model.Id,
                supportingTableName = catName,
                description = null,
                textField = null,
                parentID = string.IsNullOrWhiteSpace(model.DepartmentId) ? null : model.DepartmentId.Trim(),
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
                ? (isNew ? $"Category '{catName}' added successfully." : $"Category '{catName}' updated successfully.")
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to save item category." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

        public async Task<(bool Success, string Message)> DeleteCategoryAsync(string id)
        {
            var (success, rawMessage) = await _supportingTableService.DeleteAsync(id);
            string userMessage = success
                ? "Category deleted successfully."
                : (string.IsNullOrWhiteSpace(rawMessage) ? "Failed to delete item category." : rawMessage);

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

    }
}
