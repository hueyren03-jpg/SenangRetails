using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class ItemCategoryModel
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>System Code / ID (mapped to API SupportingTableID, e.g. HQ0000000000127)</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>Category Name (mapped to API SupportingTableName, e.g. Toys)</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Alias for Name for backwards compatibility</summary>
        public string Description { get => Name; set => Name = value; }
        /// <summary>Parent Item Department ID (mapped to API ParentID)</summary>
        public string DepartmentId { get; set; } = string.Empty;
        /// <summary>Resolved name of the parent department</summary>
        public string DepartmentName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Sequence { get; set; } = 0;
        public DateTime? CreatedDateTime { get; set; }
    }
}
