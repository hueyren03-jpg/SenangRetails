using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.ItemDivisionService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services.ItemDepartmentService
{
    public class ItemDepartmentService : IItemDepartmentService
    {
        private const int SupportingTableTypeId = 54; // EnumSupportingTableType.ItemDepartment
        private readonly ISupportingTableService _supportingTableService;
        private readonly IItemDivisionService _divisionService;

        public ItemDepartmentService(
            ISupportingTableService supportingTableService,
            IItemDivisionService divisionService)
        {
            _supportingTableService = supportingTableService;
            _divisionService = divisionService;
        }

        private static ItemDepartmentModel MapItem(SupportingTableItem x, Dictionary<string, string> divisionMap)
        {
            var divId = x.ParentID ?? string.Empty;
            divisionMap.TryGetValue(divId, out var divName);

            return new ItemDepartmentModel
            {
                Id = x.SupportingTableID ?? string.Empty,
                Code = x.SupportingTableID ?? string.Empty,
                Name = x.SupportingTableName ?? string.Empty,
                DivisionId = divId,
                DivisionName = divName ?? string.Empty,
                Active = x.Active,
                Sequence = x.Sequence,
                CreatedDateTime = DateTime.TryParse(x.CreatedDateTime, out var dt) ? dt : null
            };
        }

        public async Task<List<ItemDepartmentModel>> GetDepartmentsAsync()
        {
            try
            {
                var divisionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var divisions = await _divisionService.GetDivisionsAsync();
                    foreach (var d in divisions)
                    {
                        if (!string.IsNullOrEmpty(d.Id))
                        {
                            divisionMap[d.Id] = !string.IsNullOrWhiteSpace(d.Name) ? d.Name : d.Code;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ItemDepartmentService] Failed to load divisions for mapping: {ex.Message}");
                }

                var apiItems = await _supportingTableService.LoadListByTypeAsync(SupportingTableTypeId);
                if (apiItems != null)
                {
                    return apiItems.Select(x => MapItem(x, divisionMap)).OrderBy(x => x.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDepartmentService] Failed to load departments from API: {ex.Message}");
            }

            return new List<ItemDepartmentModel>();
        }

        public async Task<(bool Success, string Message)> SaveDepartmentAsync(ItemDepartmentModel model)
        {
            bool isNew = string.IsNullOrWhiteSpace(model.Id);
            var deptName = (model.Name ?? model.Description ?? string.Empty).Trim();
            var supportingModel = new SupportingTableModel
            {
                supportingTableID = isNew ? null : model.Id,
                supportingTableName = deptName,
                description = null,
                textField = null,
                parentID = string.IsNullOrWhiteSpace(model.DivisionId) ? null : model.DivisionId.Trim(),
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
                ? (isNew ? "Department created successfully." : "Department updated successfully.")
                : (!string.IsNullOrWhiteSpace(rawMessage) ? rawMessage.Replace("category", "department").Replace("Category", "Department") : "Failed to save department.");

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }

        public async Task<(bool Success, string Message)> DeleteDepartmentAsync(string id)
        {
            var (success, rawMessage) = await _supportingTableService.DeleteAsync(id);
            string userMessage = success
                ? "Department deleted successfully."
                : (!string.IsNullOrWhiteSpace(rawMessage) ? rawMessage.Replace("category", "department").Replace("Category", "Department") : "Failed to delete department.");

            if (success)
            {
                return (true, userMessage);
            }

            return (success, userMessage);
        }
    }
}
