using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.Sync
{
    public sealed record SaleSyncResult(
        bool Synced,
        bool RequiresReview,
        string Message,
        string? ServerDisplayCode = null,
        string? ServerDocumentId = null);

    public interface IOrderSyncService
    {
        bool IsSyncing { get; }
        event Action? OnSyncStatusChanged;
        Task<SaleSyncResult> SyncSaleAsync(string localId);
        Task<(int TotalSynced, int TotalFailed, List<string> ErrorMessages)> SyncPendingOrdersAsync(bool ignoreSchedule = false);
        Task<int> GetPendingCountAsync();
        Task TryAutoSyncAsync();
    }
}
