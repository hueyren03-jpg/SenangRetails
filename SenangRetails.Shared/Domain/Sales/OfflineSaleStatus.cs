namespace SenangRetails.Shared.Domain.Sales;

public enum OfflineSaleStatus
{
    PendingSync = 0,
    Synced = 1,
    RetryScheduled = 2,
    RequiresReview = 3
}
