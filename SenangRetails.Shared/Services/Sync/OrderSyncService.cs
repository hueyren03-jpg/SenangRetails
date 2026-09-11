using System.Text.Json;
using EBI.UC;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Services.Connectivity;
using SenangRetails.Shared.Services.DataLayer.Offline;

namespace SenangRetails.Shared.Services.Sync;

public sealed class OrderSyncService : IOrderSyncService, IDisposable
{
    private readonly IOfflineCashSalesStorage _offlineStorage;
    private readonly CashSalesAC _cashSalesApi;
    private readonly INetworkStatusService _network;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _periodicSyncTask;

    public bool IsSyncing => _syncGate.CurrentCount == 0;
    public event Action? OnSyncStatusChanged;

    public OrderSyncService(
        IOfflineCashSalesStorage offlineStorage,
        CashSalesAC cashSalesApi,
        INetworkStatusService network)
    {
        _offlineStorage = offlineStorage;
        _cashSalesApi = cashSalesApi;
        _network = network;
        _periodicSyncTask = Task.Run(() => RunPeriodicSyncAsync(_shutdown.Token));
    }

    public async Task TryAutoSyncAsync()
    {
        if (_network.IsInternetAvailable)
            await SyncPendingOrdersAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        try
        {
            return await _offlineStorage.GetPendingCountAsync();
        }
        catch
        {
            return 0;
        }
    }

    public async Task<SaleSyncResult> SyncSaleAsync(string localId)
    {
        if (!_network.IsInternetAvailable)
            return new(false, false, "Saved locally and waiting for internet connection.");
        if (!await _syncGate.WaitAsync(0))
            return new(false, false, "Saved locally; synchronization is already running.");

        NotifyStateChanged();
        try
        {
            var sale = await _offlineStorage.GetSaleByLocalIdAsync(localId);
            if (sale is null)
                return new(false, true, "The local transaction could not be found.");
            if (sale.Status == SyncStatus.Synced
                && sale.IsOnlineVisibilityConfirmed
                && !string.IsNullOrWhiteSpace(sale.ServerDocumentId)
                && !string.IsNullOrWhiteSpace(sale.ServerDisplayCode))
                return new(true, false, "Already synchronized.", sale.ServerDisplayCode, sale.ServerDocumentId);
            return await SubmitAsync(sale);
        }
        finally
        {
            _syncGate.Release();
            NotifyStateChanged();
        }
    }

    public async Task<(int TotalSynced, int TotalFailed, List<string> ErrorMessages)> SyncPendingOrdersAsync(
        bool ignoreSchedule = false)
    {
        if (!_network.IsInternetAvailable)
            return (0, 0, ["Internet connection is not available."]);
        if (!await _syncGate.WaitAsync(0))
            return (0, 0, ["Synchronization is already in progress."]);

        NotifyStateChanged();
        var synced = 0;
        var failed = 0;
        var errors = new List<string>();
        try
        {
            var pendingSales = await _offlineStorage.GetPendingOfflineSalesAsync(ignoreSchedule);
            foreach (var sale in pendingSales)
            {
                if (!_network.IsInternetAvailable)
                    break;
                var result = await SubmitAsync(sale);
                if (result.Synced)
                    synced++;
                else
                {
                    failed++;
                    errors.Add($"Order {sale.LocalDisplayCode}: {result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Synchronization failed: {ex.Message}");
        }
        finally
        {
            _syncGate.Release();
            NotifyStateChanged();
        }
        return (synced, failed, errors);
    }

    private async Task<SaleSyncResult> SubmitAsync(OfflineCashSaleEntity sale)
    {
        await _offlineStorage.MarkSaleAttemptStartedAsync(sale.LocalId, DateTime.UtcNow);
        Doc_CashSales? request;
        try
        {
            request = JsonSerializer.Deserialize<Doc_CashSales>(sale.OrderPayloadJson);
        }
        catch (Exception ex)
        {
            var message = $"Invalid local transaction payload: {ex.Message}";
            await _offlineStorage.MarkSaleRequiresReviewAsync(sale.LocalId, message);
            return new(false, true, message);
        }

        if (request?.objDoc_CashSales is null)
        {
            const string message = "The local transaction payload is incomplete.";
            await _offlineStorage.MarkSaleRequiresReviewAsync(sale.LocalId, message);
            return new(false, true, message);
        }

        var normalizationError = NormalizeForServer(request, sale);
        if (normalizationError is not null)
        {
            await _offlineStorage.MarkSaleRequiresReviewAsync(sale.LocalId, normalizationError);
            return new(false, true, normalizationError);
        }
        try
        {
            var existing = await TryLoadServerSaleAsync([sale.ServerDocumentId, sale.LocalId], sale.BranchId);
            if (existing is not null)
                return await ConfirmWhenReportVisibleAsync(
                    sale, existing.Value.DocumentId, existing.Value.DisplayCode);

            var response = await _cashSalesApi.CreateCashSalesRecordAsync(request);
            var statusCode = response?.StatusCode ?? 0;
            if (statusCode is >= 200 and < 300 && response is { IsError: false, Result: not null })
            {
                var persisted = await TryLoadServerSaleAsync(
                    [response.Result.Id, sale.LocalId], sale.BranchId, retryForVisibility: true);
                if (persisted is not null)
                    return await ConfirmWhenReportVisibleAsync(
                        sale, persisted.Value.DocumentId, persisted.Value.DisplayCode, retryForVisibility: true);

                const string unconfirmedMessage =
                    "The server accepted the order but it could not be read back. It remains queued for confirmation.";
                await _offlineStorage.ScheduleSaleRetryAsync(
                    sale.LocalId, unconfirmedMessage, DateTime.UtcNow.Add(CalculateBackoff(sale.RetryCount)));
                return new(false, false, unconfirmedMessage);
            }

            var message = FirstMessage(response?.Message, response?.Detail, response?.Title)
                ?? "The server did not return a successful response.";
            if (IsRetryable(statusCode))
            {
                await _offlineStorage.ScheduleSaleRetryAsync(
                    sale.LocalId, message, DateTime.UtcNow.Add(CalculateBackoff(sale.RetryCount)));
                return new(false, false, message);
            }

            await _offlineStorage.MarkSaleRequiresReviewAsync(sale.LocalId, message);
            return new(false, true, message);
        }
        catch (Exception ex)
        {
            await _offlineStorage.ScheduleSaleRetryAsync(
                sale.LocalId, ex.Message, DateTime.UtcNow.Add(CalculateBackoff(sale.RetryCount)));
            return new(false, false, ex.Message);
        }
    }

    private static string? NormalizeForServer(Doc_CashSales request, OfflineCashSaleEntity sale)
    {
        var header = request.objDoc_CashSales!;
        var branchId = FirstNonEmpty(
            sale.BranchId,
            header.BranchID,
            header.EditBranchID,
            request.lstDocumentLine?.FirstOrDefault()?.BranchID,
            request.lstDocumentLine?.FirstOrDefault()?.EditBranchID,
            request.lstReceiptLines?.FirstOrDefault()?.BranchID);

        if (string.IsNullOrWhiteSpace(branchId))
            return "The offline transaction has no branch ID and cannot be synchronized. Return to the branch selection screen and create a new sale.";

        var editBranchId = branchId;

        var transactionTime = sale.FinancialDate == default
            ? sale.CreatedAtUtc.ToLocalTime()
            : sale.FinancialDate;

        header.DocumentID = sale.LocalId;
        header.BranchID = branchId;
        header.EditBranchID = editBranchId;
        if (header.FinancialDate == default)
            header.FinancialDate = transactionTime;
        if (header.CreatedDateTime == default)
            header.CreatedDateTime = transactionTime;
        if (header.UpdateTimeStamp == default)
            header.UpdateTimeStamp = transactionTime;
        if (header.TablePaidTime == default)
            header.TablePaidTime = transactionTime;

        foreach (var line in request.lstDocumentLine ?? [])
        {
            line.BranchID = branchId;
            line.EditBranchID = editBranchId;
            if (line.FinancialDate == default)
                line.FinancialDate = transactionTime;
        }

        foreach (var receipt in request.lstReceiptLines ?? [])
        {
            receipt.BranchID = branchId;
            if (receipt.FinancialDate == default)
                receipt.FinancialDate = transactionTime;
        }

        return null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private async Task<(string DocumentId, string DisplayCode)?> TryLoadServerSaleAsync(
        IEnumerable<string?> candidateIds,
        string expectedBranchId,
        bool retryForVisibility = false)
    {
        var ids = candidateIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var delays = retryForVisibility
            ? new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1) }
            : new[] { TimeSpan.Zero };

        foreach (var delay in delays)
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            foreach (var id in ids)
            {
                var response = await _cashSalesApi.LoadRecordAsync(id);
                var header = response?.Result?.objDoc_CashSales;
                if (response?.StatusCode is >= 200 and < 300
                    && response.IsError == false
                    && header is not null
                    && !string.IsNullOrWhiteSpace(header.DocumentID)
                    && (string.IsNullOrWhiteSpace(expectedBranchId)
                        || string.Equals(header.BranchID, expectedBranchId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (
                        header.DocumentID,
                        string.IsNullOrWhiteSpace(header.DisplayCode) ? id : header.DisplayCode);
                }
            }
        }

        return null;
    }

    private async Task<SaleSyncResult> ConfirmSyncedAsync(
        OfflineCashSaleEntity sale,
        string serverDocumentId,
        string serverDisplayCode)
    {
        var displayCode = string.IsNullOrWhiteSpace(serverDisplayCode)
            ? sale.LocalDisplayCode
            : serverDisplayCode;
        await _offlineStorage.MarkSaleSyncedAsync(sale.LocalId, serverDocumentId, displayCode);
        return new(true, false, "Synchronized and confirmed by the server.", displayCode, serverDocumentId);
    }

    private async Task<SaleSyncResult> ConfirmWhenReportVisibleAsync(
        OfflineCashSaleEntity sale,
        string serverDocumentId,
        string serverDisplayCode,
        bool retryForVisibility = false)
    {
        var reportSale = await TryLoadServerReportSaleAsync(
            sale,
            [serverDocumentId, sale.LocalId],
            [serverDisplayCode, sale.ServerDisplayCode],
            retryForVisibility);
        if (reportSale is not null)
            return await ConfirmSyncedAsync(
                sale,
                reportSale.Value.DocumentId,
                reportSale.Value.DisplayCode);

        const string message =
            "The server saved the order, but it is not visible in online reports yet. Report confirmation will be retried without creating another order.";
        await _offlineStorage.ScheduleSaleRetryAsync(
            sale.LocalId,
            message,
            DateTime.UtcNow.Add(CalculateBackoff(sale.RetryCount)));
        return new(false, false, message, serverDisplayCode, serverDocumentId);
    }

    private async Task<(string DocumentId, string DisplayCode)?> TryLoadServerReportSaleAsync(
        OfflineCashSaleEntity sale,
        IEnumerable<string?> candidateIds,
        IEnumerable<string?> candidateDisplayCodes,
        bool retryForVisibility)
    {
        var ids = candidateIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var displayCodes = candidateDisplayCodes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var delays = retryForVisibility
            ? new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2) }
            : new[] { TimeSpan.Zero };

        foreach (var delay in delays)
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            var financialDate = sale.FinancialDate == default
                ? sale.CreatedAtUtc.ToLocalTime()
                : sale.FinancialDate;
            var response = await _cashSalesApi.GetCashSalesReportAsync(
                sale.BranchId,
                financialDate.Date,
                financialDate.Date.AddDays(1).AddTicks(-1));
            if (response?.StatusCode is not (>= 200 and < 300)
                || response.IsError
                || response.Result is null)
                continue;

            var match = response.Result.FirstOrDefault(row =>
                (!string.IsNullOrWhiteSpace(row.DocumentID) && ids.Contains(row.DocumentID))
                || (!string.IsNullOrWhiteSpace(row.DisplayCode) && displayCodes.Contains(row.DisplayCode)));
            if (match is not null)
            {
                return (
                    string.IsNullOrWhiteSpace(match.DocumentID)
                        ? ids.FirstOrDefault() ?? sale.ServerDocumentId ?? sale.LocalId
                        : match.DocumentID,
                    string.IsNullOrWhiteSpace(match.DisplayCode)
                        ? displayCodes.FirstOrDefault() ?? sale.ServerDisplayCode ?? sale.LocalDisplayCode
                        : match.DisplayCode);
            }
        }

        return null;
    }

    private static string? FirstMessage(params string?[] messages) =>
        messages.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

    private static bool IsRetryable(int statusCode) =>
        statusCode == 0 || statusCode is 401 or 403 or 408 or 425 or 429 || statusCode >= 500;

    private static TimeSpan CalculateBackoff(int retryCount)
    {
        var exponent = Math.Min(Math.Max(retryCount, 0), 6);
        return TimeSpan.FromSeconds(Math.Min(15 * Math.Pow(2, exponent), 900));
    }

    private async Task RunPeriodicSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await TryAutoSyncAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void NotifyStateChanged() => OnSyncStatusChanged?.Invoke();

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _syncGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
