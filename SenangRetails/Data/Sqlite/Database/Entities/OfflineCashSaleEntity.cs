namespace SenangRetails.Data.Sqlite.Database.Entities;

internal sealed class OfflineCashSaleEntity
{
    public string LocalId { get; set; } = string.Empty;
    public string LocalDisplayCode { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string OrderPayloadJson { get; set; } = string.Empty;
    public string PaymentLinesJson { get; set; } = "[]";
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public string? ServerDocumentId { get; set; }
    public string? ServerDisplayCode { get; set; }
    public bool IsOnlineVisibilityConfirmed { get; set; }
    public string? LastErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}
