using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Domain.Sales;

namespace SenangRetails.Shared.Services.DataLayer.Online;

internal sealed class OnlineOnlyOfflineCashSaleRepository : IOfflineCashSaleRepository
{
    private const string OfflineNotAvailable = "Offline cash-sale storage is not available in the web application.";

    public Task AddAsync(OfflineCashSale sale, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(OfflineNotAvailable));

    public Task<IReadOnlyList<OfflineCashSale>> GetPendingAsync(DateTime utcNow, bool ignoreSchedule = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OfflineCashSale>>([]);

    public Task<IReadOnlyList<OfflineCashSale>> GetForDateAsync(string branchId, DateTime date, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OfflineCashSale>>([]);

    public Task<OfflineCashSale?> GetByLocalIdAsync(string localId, CancellationToken cancellationToken = default) =>
        Task.FromResult<OfflineCashSale?>(null);

    public Task<bool> MarkSyncedAsync(string localId, string serverDocumentId, string serverDisplayCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> MarkAttemptStartedAsync(string localId, DateTime attemptedAtUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ScheduleRetryAsync(string localId, string errorMessage, DateTime nextAttemptAtUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> MarkRequiresReviewAsync(string localId, string errorMessage, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<bool> DeleteAsync(string localId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
