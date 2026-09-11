using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.CashDrawerService;

public sealed class CashDrawerService(ILocalJsonCache localCache) : ICashDrawerService
{
    private const string StorageKey = "cash-drawer:transactions:v1";
    private const string LegacyStorageKey = "senang_cash_drawer_logs";
    private static readonly SemaphoreSlim TransactionGate = new(1, 1);

    public async Task<IReadOnlyList<CashDrawerLogModel>> GetLogsAsync(
        string branchId,
        string counter,
        DateTime? businessDate = null,
        CancellationToken cancellationToken = default)
    {
        branchId = NormalizeRequired(branchId);
        counter = NormalizeRequired(counter);

        var store = await LoadStoreAsync(cancellationToken);
        return store.Transactions
            .Where(x => MatchesScope(x, branchId, counter))
            .Where(x => !businessDate.HasValue || x.Timestamp.Date == businessDate.Value.Date)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public async Task<CashDrawerSummaryModel> GetSummaryAsync(
        string branchId,
        string counter,
        DateTime businessDate,
        CancellationToken cancellationToken = default)
    {
        var logs = await GetLogsAsync(branchId, counter, businessDate, cancellationToken);
        var cashIn = logs.Where(x => IsType(x, CashDrawerTransactionTypes.CashIn)).ToList();

        return new CashDrawerSummaryModel
        {
            BranchId = NormalizeRequired(branchId),
            Counter = NormalizeRequired(counter),
            BusinessDate = businessDate.Date,
            TransactionCount = logs.Count,
            OpeningFloat = cashIn.Where(x => IsOpeningFloat(x.Reason)).Sum(x => x.Amount),
            TotalCashInToday = cashIn.Where(x => !IsOpeningFloat(x.Reason)).Sum(x => x.Amount),
            TotalCashOutToday = logs
                .Where(x => IsType(x, CashDrawerTransactionTypes.CashOut))
                .Sum(x => x.Amount)
        };
    }

    public async Task<CashDrawerOperationResult> RecordTransactionAsync(
        CashDrawerLogModel transaction,
        CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateAndNormalize(transaction);
        if (validationMessage is not null)
        {
            return CashDrawerOperationResult.Failed(validationMessage);
        }

        await TransactionGate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (store.Transactions.Any(x => string.Equals(x.Id, transaction.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return CashDrawerOperationResult.Failed("This cash drawer transaction has already been recorded.");
            }

            var sameDrawerDay = store.Transactions
                .Where(x => MatchesScope(x, transaction.BranchId, transaction.Counter))
                .Where(x => x.Timestamp.Date == transaction.Timestamp.Date)
                .ToList();

            if (IsType(transaction, CashDrawerTransactionTypes.CashIn) &&
                IsOpeningFloat(transaction.Reason) &&
                sameDrawerDay.Any(x => IsType(x, CashDrawerTransactionTypes.CashIn) && IsOpeningFloat(x.Reason)))
            {
                return CashDrawerOperationResult.Failed(
                    "An opening float already exists for this branch, counter, and business date.");
            }

            if (IsType(transaction, CashDrawerTransactionTypes.CashOut))
            {
                var availableBalance = CalculateBalance(sameDrawerDay);
                if (transaction.Amount > availableBalance)
                {
                    return CashDrawerOperationResult.Failed(
                        $"Cash out cannot exceed the available drawer balance of RM {availableBalance:N2}.");
                }
            }

            store.Transactions.Add(transaction);
            await localCache.SetAsync(StorageKey, store, cancellationToken);
            return CashDrawerOperationResult.Succeeded(transaction);
        }
        catch (Exception ex)
        {
            return CashDrawerOperationResult.Failed($"The cash drawer transaction could not be saved locally: {ex.Message}");
        }
        finally
        {
            TransactionGate.Release();
        }
    }

    private async Task<CashDrawerStore> LoadStoreAsync(CancellationToken cancellationToken)
    {
        var store = await localCache.GetAsync<CashDrawerStore>(StorageKey, cancellationToken);
        if (store is not null)
        {
            store.Transactions ??= [];
            return store;
        }

        var legacyLogs = await localCache.GetAsync<List<CashDrawerLogModel>>(LegacyStorageKey, cancellationToken);
        var migratedLogs = legacyLogs?
            .Where(x => !IsDemoSeed(x))
            .Select(NormalizeLegacyTransaction)
            .ToList() ?? [];

        store = new CashDrawerStore { Transactions = migratedLogs };
        await localCache.SetAsync(StorageKey, store, cancellationToken);
        return store;
    }

    private static string? ValidateAndNormalize(CashDrawerLogModel transaction)
    {
        if (transaction is null)
        {
            return "Cash drawer transaction details are required.";
        }

        transaction.Id = string.IsNullOrWhiteSpace(transaction.Id) ? Guid.NewGuid().ToString() : transaction.Id.Trim();
        transaction.Type = transaction.Type?.Trim() ?? string.Empty;
        transaction.Reason = transaction.Reason?.Trim() ?? string.Empty;
        transaction.Notes = transaction.Notes?.Trim() ?? string.Empty;
        transaction.PerformedBy = transaction.PerformedBy?.Trim() ?? string.Empty;
        transaction.BranchId = transaction.BranchId?.Trim() ?? string.Empty;
        transaction.Branch = transaction.Branch?.Trim() ?? string.Empty;
        transaction.Counter = transaction.Counter?.Trim() ?? string.Empty;
        transaction.CreatedAtUtc = transaction.CreatedAtUtc == default ? DateTime.UtcNow : transaction.CreatedAtUtc;
        transaction.SyncStatus = CashDrawerSyncStatus.LocalOnly;
        transaction.ServerId = null;
        transaction.SyncedAtUtc = null;

        if (!IsType(transaction, CashDrawerTransactionTypes.CashIn) &&
            !IsType(transaction, CashDrawerTransactionTypes.CashOut))
        {
            return "Transaction type must be Cash In or Cash Out.";
        }

        if (transaction.Amount <= 0 || transaction.Amount > 999_999_999.99m)
        {
            return "Enter an amount greater than RM 0.00 and below RM 1,000,000,000.00.";
        }

        if (string.IsNullOrWhiteSpace(transaction.Reason)) return "Select a reason for this transaction.";
        if (string.IsNullOrWhiteSpace(transaction.PerformedBy)) return "Performed By is required.";
        if (string.IsNullOrWhiteSpace(transaction.BranchId)) return "A branch must be selected before recording cash movement.";
        if (string.IsNullOrWhiteSpace(transaction.Counter)) return "Counter is required.";
        if (transaction.Timestamp == default) return "Transaction date and time are required.";
        if (transaction.Timestamp > DateTime.Now.AddMinutes(1)) return "Transaction date and time cannot be in the future.";

        transaction.Branch = string.IsNullOrWhiteSpace(transaction.Branch) ? transaction.BranchId : transaction.Branch;
        return null;
    }

    private static CashDrawerLogModel NormalizeLegacyTransaction(CashDrawerLogModel transaction)
    {
        transaction.BranchId = string.IsNullOrWhiteSpace(transaction.BranchId)
            ? NormalizeRequired(transaction.Branch)
            : NormalizeRequired(transaction.BranchId);
        transaction.Branch = string.IsNullOrWhiteSpace(transaction.Branch) ? transaction.BranchId : transaction.Branch.Trim();
        transaction.Counter = string.IsNullOrWhiteSpace(transaction.Counter) ? "Counter 1" : transaction.Counter.Trim();
        transaction.CreatedAtUtc = transaction.CreatedAtUtc == default ? DateTime.UtcNow : transaction.CreatedAtUtc;
        transaction.SyncStatus = CashDrawerSyncStatus.LocalOnly;
        return transaction;
    }

    private static bool MatchesScope(CashDrawerLogModel transaction, string branchId, string counter) =>
        string.Equals(transaction.BranchId, branchId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(transaction.Counter, counter, StringComparison.OrdinalIgnoreCase);

    private static bool IsType(CashDrawerLogModel transaction, string type) =>
        string.Equals(transaction.Type, type, StringComparison.OrdinalIgnoreCase);

    private static bool IsOpeningFloat(string reason) =>
        string.Equals(reason, CashDrawerReasons.OpeningFloat, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reason, "Starting Float", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reason, "Opening Balance", StringComparison.OrdinalIgnoreCase);

    private static decimal CalculateBalance(IEnumerable<CashDrawerLogModel> transactions)
    {
        var cashIn = transactions.Where(x => IsType(x, CashDrawerTransactionTypes.CashIn)).Sum(x => x.Amount);
        var cashOut = transactions.Where(x => IsType(x, CashDrawerTransactionTypes.CashOut)).Sum(x => x.Amount);
        return cashIn - cashOut;
    }

    private static bool IsDemoSeed(CashDrawerLogModel transaction) =>
        transaction.Amount == 200m &&
        string.Equals(transaction.Reason, "Starting Float", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(transaction.Notes, "Morning register float top up", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(transaction.PerformedBy, "Manager", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRequired(string value) => value?.Trim() ?? string.Empty;

    private sealed class CashDrawerStore
    {
        public int SchemaVersion { get; set; } = 1;
        public List<CashDrawerLogModel> Transactions { get; set; } = [];
    }
}
