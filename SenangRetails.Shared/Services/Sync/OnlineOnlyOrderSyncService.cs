namespace SenangRetails.Shared.Services.Sync;

public sealed class OnlineOnlyOrderSyncService : IOrderSyncService
{
    public bool IsSyncing => false;
    public event Action? OnSyncStatusChanged
    {
        add { }
        remove { }
    }

    public Task<SaleSyncResult> SyncSaleAsync(string localId) =>
        Task.FromResult(new SaleSyncResult(false, true, "Local synchronization is not used by the web application."));

    public Task<(int TotalSynced, int TotalFailed, List<string> ErrorMessages)> SyncPendingOrdersAsync(
        bool ignoreSchedule = false) =>
        Task.FromResult((0, 0, new List<string>()));

    public Task<int> GetPendingCountAsync() => Task.FromResult(0);

    public Task TryAutoSyncAsync() => Task.CompletedTask;
}
