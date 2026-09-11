namespace SenangRetails.Shared.Domain.Sales;

public sealed class OfflineCashSale
{
    public string LocalId { get; private set; }
    public string LocalDisplayCode { get; private set; }
    public string BranchId { get; private set; }
    public DateTime FinancialDate { get; private set; }
    public string AccountId { get; private set; }
    public string AccountName { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int ItemCount { get; private set; }
    public string OrderPayloadJson { get; private set; }
    public string PaymentLinesJson { get; private set; }
    public OfflineSaleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SyncedAtUtc { get; private set; }
    public string? ServerDocumentId { get; private set; }
    public string? ServerDisplayCode { get; private set; }
    public bool IsOnlineVisibilityConfirmed { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }

    private OfflineCashSale(
        string localId, string localDisplayCode, string branchId, DateTime financialDate,
        string accountId, string accountName, decimal totalAmount, int itemCount,
        string orderPayloadJson, string paymentLinesJson, OfflineSaleStatus status,
        DateTime createdAtUtc, DateTime? syncedAtUtc, string? serverDocumentId,
        string? serverDisplayCode, bool isOnlineVisibilityConfirmed,
        string? lastErrorMessage, int retryCount,
        DateTime? lastAttemptAtUtc, DateTime? nextAttemptAtUtc)
    {
        LocalId = localId;
        LocalDisplayCode = localDisplayCode;
        BranchId = branchId;
        FinancialDate = financialDate;
        AccountId = accountId;
        AccountName = accountName;
        TotalAmount = totalAmount;
        ItemCount = itemCount;
        OrderPayloadJson = orderPayloadJson;
        PaymentLinesJson = paymentLinesJson;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        SyncedAtUtc = syncedAtUtc;
        ServerDocumentId = serverDocumentId;
        ServerDisplayCode = serverDisplayCode;
        IsOnlineVisibilityConfirmed = isOnlineVisibilityConfirmed;
        LastErrorMessage = lastErrorMessage;
        RetryCount = retryCount;
        LastAttemptAtUtc = lastAttemptAtUtc;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public static OfflineCashSale CreatePending(
        string localDisplayCode, string branchId, DateTime financialDate,
        string accountId, string accountName, decimal totalAmount, int itemCount,
        string orderPayloadJson, string paymentLinesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localDisplayCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderPayloadJson);

        return new OfflineCashSale(
            Guid.NewGuid().ToString(), localDisplayCode.Trim(), branchId?.Trim() ?? string.Empty,
            financialDate, accountId?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(accountName) ? "Walk-In Customer" : accountName.Trim(),
            totalAmount, itemCount, orderPayloadJson,
            string.IsNullOrWhiteSpace(paymentLinesJson) ? "[]" : paymentLinesJson,
            OfflineSaleStatus.PendingSync, DateTime.UtcNow, null, null, null, false, null, 0, null, null);
    }

    public static OfflineCashSale Restore(
        string localId, string localDisplayCode, string branchId, DateTime financialDate,
        string accountId, string accountName, decimal totalAmount, int itemCount,
        string orderPayloadJson, string paymentLinesJson, OfflineSaleStatus status,
        DateTime createdAtUtc, DateTime? syncedAtUtc, string? serverDocumentId,
        string? serverDisplayCode, bool isOnlineVisibilityConfirmed,
        string? lastErrorMessage, int retryCount,
        DateTime? lastAttemptAtUtc, DateTime? nextAttemptAtUtc) => new(
            localId, localDisplayCode, branchId, financialDate, accountId, accountName,
            totalAmount, itemCount, orderPayloadJson, paymentLinesJson, status, createdAtUtc,
            syncedAtUtc, serverDocumentId, serverDisplayCode, isOnlineVisibilityConfirmed,
            lastErrorMessage, retryCount,
            lastAttemptAtUtc, nextAttemptAtUtc);

    public void MarkSynced(string serverDocumentId, string serverDisplayCode)
    {
        Status = OfflineSaleStatus.Synced;
        SyncedAtUtc = DateTime.UtcNow;
        ServerDocumentId = serverDocumentId;
        ServerDisplayCode = serverDisplayCode;
        IsOnlineVisibilityConfirmed = true;
        LastErrorMessage = null;
        NextAttemptAtUtc = null;
    }

    public void ScheduleRetry(string errorMessage, DateTime nextAttemptAtUtc)
    {
        Status = OfflineSaleStatus.RetryScheduled;
        LastErrorMessage = errorMessage;
        RetryCount++;
        LastAttemptAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public void RequireReview(string errorMessage)
    {
        Status = OfflineSaleStatus.RequiresReview;
        LastErrorMessage = errorMessage;
        RetryCount++;
        LastAttemptAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = null;
    }
}
