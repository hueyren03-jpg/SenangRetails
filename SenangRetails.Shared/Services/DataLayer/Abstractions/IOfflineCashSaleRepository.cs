using SenangRetails.Shared.Domain.Sales;

namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public interface IOfflineCashSaleRepository
{
    Task AddAsync(OfflineCashSale sale, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfflineCashSale>> GetPendingAsync(DateTime utcNow, bool ignoreSchedule = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfflineCashSale>> GetForDateAsync(string branchId, DateTime date, CancellationToken cancellationToken = default);
    Task<OfflineCashSale?> GetByLocalIdAsync(string localId, CancellationToken cancellationToken = default);
    Task<bool> MarkSyncedAsync(string localId, string serverDocumentId, string serverDisplayCode, CancellationToken cancellationToken = default);
    Task<bool> MarkAttemptStartedAsync(string localId, DateTime attemptedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> ScheduleRetryAsync(string localId, string errorMessage, DateTime nextAttemptAtUtc, CancellationToken cancellationToken = default);
    Task<bool> MarkRequiresReviewAsync(string localId, string errorMessage, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string localId, CancellationToken cancellationToken = default);
}
