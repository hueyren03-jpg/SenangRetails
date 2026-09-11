using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class ItemBrandModel
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>System Code / ID (mapped to API SupportingTableID, e.g. HQ0000000000129)</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>Brand Name / Description (mapped to API SupportingTableName)</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Alias for Name for backwards compatibility</summary>
        public string Description { get => Name; set => Name = value; }
        public bool Active { get; set; } = true;
        public int Sequence { get; set; } = 0;
        public DateTime? CreatedDateTime { get; set; }
    }
}
