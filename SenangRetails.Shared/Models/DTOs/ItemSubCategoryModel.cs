using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class ItemSubCategoryModel
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>System Code / ID (mapped to API SupportingTableID, e.g. HQ0000000000128)</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>Sub Category Name (mapped to API SupportingTableName)</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Alias for Name for backwards compatibility</summary>
        public string Description { get => Name; set => Name = value; }
        /// <summary>Parent Item Category ID (mapped to API ParentID)</summary>
        public string CategoryId { get; set; } = string.Empty;
        /// <summary>Resolved name of the parent category</summary>
        public string CategoryName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Sequence { get; set; } = 0;
        public DateTime? CreatedDateTime { get; set; }
    }
}
