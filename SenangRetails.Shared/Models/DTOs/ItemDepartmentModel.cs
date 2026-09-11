using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class ItemDepartmentModel
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>System Code / ID (mapped to API SupportingTableID, e.g. HQ0000000000898)</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>Department Name (mapped to API SupportingTableName, e.g. Department 1)</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Alias for Name for backwards compatibility</summary>
        public string Description { get => Name; set => Name = value; }
        public string DivisionId { get; set; } = string.Empty;
        public string DivisionName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Sequence { get; set; } = 0;
        public DateTime? CreatedDateTime { get; set; }
    }
}
