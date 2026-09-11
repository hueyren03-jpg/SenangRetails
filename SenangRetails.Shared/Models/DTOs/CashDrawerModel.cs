using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public static class CashDrawerTransactionTypes
    {
        public const string CashIn = "Cash In";
        public const string CashOut = "Cash Out";
    }

    public static class CashDrawerReasons
    {
        public const string OpeningFloat = "Opening Float";
        public const string CashReplenishment = "Cash Replenishment";
        public const string PettyCashReturn = "Petty Cash Return";
        public const string PettyCashExpense = "Petty Cash Expense";
        public const string BankDeposit = "Bank Deposit";
        public const string SupplierPayment = "Supplier Payment";
        public const string CashRefund = "Cash Refund";
        public const string ChangeFundAdjustment = "Change Fund Adjustment";
        public const string Other = "Other";
    }

    public enum CashDrawerSyncStatus
    {
        LocalOnly = 0,
        Pending = 1,
        Synced = 2,
        Failed = 3
    }

    public class CashDrawerLogModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? ServerId { get; set; }
        public string Type { get; set; } = CashDrawerTransactionTypes.CashIn;
        public decimal Amount { get; set; }
        public string Reason { get; set; } = CashDrawerReasons.OpeningFloat;
        public string Notes { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = "Staff";
        public string BranchId { get; set; } = "HQ";
        public string Branch { get; set; } = "HQ";
        public string Counter { get; set; } = "Counter 1";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public CashDrawerSyncStatus SyncStatus { get; set; } = CashDrawerSyncStatus.LocalOnly;
        public DateTime? SyncedAtUtc { get; set; }
    }

    public class CashDrawerSummaryModel
    {
        public string BranchId { get; set; } = string.Empty;
        public string Counter { get; set; } = string.Empty;
        public DateTime BusinessDate { get; set; }
        public int TransactionCount { get; set; }
        public decimal OpeningFloat { get; set; }
        public decimal TotalCashInToday { get; set; }
        public decimal TotalCashOutToday { get; set; }
        public decimal CurrentDrawerBalance => OpeningFloat + TotalCashInToday - TotalCashOutToday;
    }

    public class CashDrawerOperationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public CashDrawerLogModel? Transaction { get; init; }

        public static CashDrawerOperationResult Succeeded(CashDrawerLogModel transaction) => new()
        {
            Success = true,
            Message = "Cash drawer transaction recorded locally.",
            Transaction = transaction
        };

        public static CashDrawerOperationResult Failed(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
