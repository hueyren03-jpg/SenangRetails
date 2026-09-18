using System;
using System.Collections.Generic;
using System.Linq;

namespace SenangRetails.Shared.Models.DTOs
{

    public sealed class StockTransferApiResult
    {
        public string? Id { get; set; }
        public string? DisplayCode { get; set; }
        public string? SuccessMessage { get; set; }
    }

    public sealed class StockTransferSaveResult
    {
        public bool TransferSaved { get; init; }
        public bool GinSaved { get; init; }
        public bool Success => TransferSaved && GinSaved;
        public bool IsPartialSuccess => TransferSaved && !GinSaved;
        public string Message { get; init; } = "";
        public string? TransferDocumentId { get; init; }
        public string? GinDocumentId { get; init; }
    }

    public class StockTransferModel
    {
        public string DocNo { get; set; } = "";
        public string Branch { get; set; } = "";
        public string FromBranch { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Today;
        public string Remarks { get; set; } = "";
        public List<StockTransferDetailModel> Items { get; set; } = new();

        // Helpers
        public int TotalQty => Items?.Sum(i => i.Qty) ?? 0;
        public int DistinctItemsCount => Items?.Count ?? 0;
    }

    public class StockTransferDetailModel
    {
        public string DocNo { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Matrix { get; set; } = "";
        public int Qty { get; set; } = 1;
    }
}
