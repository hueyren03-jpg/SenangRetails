using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Domain.Sales;
using SenangRetails.Data.Sqlite.Database;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Sales;

internal sealed class OfflineCashSaleRepository(IDbContextFactory<LocalAppDbContext> contextFactory)
    : IOfflineCashSaleRepository
{
    public async Task AddAsync(OfflineCashSale sale, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.OfflineCashSales
            .FirstOrDefaultAsync(x => x.LocalId == sale.LocalId, cancellationToken);
        if (existing is null)
        {
            db.OfflineCashSales.Add(ToEntity(sale));
        }
        else if (existing.Status != (int)OfflineSaleStatus.Synced)
        {
            CopyPendingValues(sale, existing);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OfflineCashSale>> GetPendingAsync(
        DateTime utcNow,
        bool ignoreSchedule = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.OfflineCashSales.AsNoTracking()
            .Where(x => x.Status == (int)OfflineSaleStatus.PendingSync
                || x.Status == (int)OfflineSaleStatus.RetryScheduled
                || (ignoreSchedule && x.Status == (int)OfflineSaleStatus.RequiresReview)
                || (x.Status == (int)OfflineSaleStatus.Synced
                    && !x.IsOnlineVisibilityConfirmed))
            .Where(x => ignoreSchedule || x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= utcNow)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<OfflineCashSale>> GetForDateAsync(
        string branchId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var start = date.Date;
        var end = start.AddDays(1);
        var rows = await db.OfflineCashSales.AsNoTracking()
            .Where(x => x.FinancialDate >= start && x.FinancialDate < end)
            .Where(x => string.IsNullOrEmpty(branchId) || x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<OfflineCashSale?> GetByLocalIdAsync(
        string localId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task<bool> MarkSyncedAsync(
        string localId,
        string serverDocumentId,
        string serverDisplayCode,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.Status = (int)OfflineSaleStatus.Synced;
        row.SyncedAtUtc = DateTime.UtcNow;
        row.ServerDocumentId = serverDocumentId;
        row.ServerDisplayCode = serverDisplayCode;
        row.IsOnlineVisibilityConfirmed = true;
        row.LastErrorMessage = null;
        row.NextAttemptAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkAttemptStartedAsync(
        string localId,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        if (row is null || (row.Status == (int)OfflineSaleStatus.Synced && row.IsOnlineVisibilityConfirmed))
            return false;

        row.LastAttemptAtUtc = attemptedAtUtc;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ScheduleRetryAsync(
        string localId,
        string errorMessage,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.Status = (int)OfflineSaleStatus.RetryScheduled;
        row.IsOnlineVisibilityConfirmed = false;
        row.LastErrorMessage = errorMessage;
        row.RetryCount++;
        row.NextAttemptAtUtc = nextAttemptAtUtc;
        row.LastAttemptAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkRequiresReviewAsync(
        string localId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        if (row is null)
            return false;

        row.Status = (int)OfflineSaleStatus.RequiresReview;
        row.LastErrorMessage = errorMessage;
        row.RetryCount++;
        row.NextAttemptAtUtc = null;
        row.LastAttemptAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.OfflineCashSales.CountAsync(
            x => x.Status != (int)OfflineSaleStatus.Synced || !x.IsOnlineVisibilityConfirmed,
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(string localId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.OfflineCashSales.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static OfflineCashSaleEntity ToEntity(OfflineCashSale sale) => new()
    {
        LocalId = sale.LocalId,
        LocalDisplayCode = sale.LocalDisplayCode,
        BranchId = sale.BranchId,
        FinancialDate = sale.FinancialDate,
        AccountId = sale.AccountId,
        AccountName = sale.AccountName,
        TotalAmount = sale.TotalAmount,
        ItemCount = sale.ItemCount,
        OrderPayloadJson = sale.OrderPayloadJson,
        PaymentLinesJson = sale.PaymentLinesJson,
        Status = (int)sale.Status,
        CreatedAtUtc = sale.CreatedAtUtc,
        SyncedAtUtc = sale.SyncedAtUtc,
        ServerDocumentId = sale.ServerDocumentId,
        ServerDisplayCode = sale.ServerDisplayCode,
        IsOnlineVisibilityConfirmed = sale.IsOnlineVisibilityConfirmed,
        LastErrorMessage = sale.LastErrorMessage,
        RetryCount = sale.RetryCount,
        LastAttemptAtUtc = sale.LastAttemptAtUtc,
        NextAttemptAtUtc = sale.NextAttemptAtUtc
    };

    private static void CopyPendingValues(OfflineCashSale sale, OfflineCashSaleEntity row)
    {
        row.LocalDisplayCode = sale.LocalDisplayCode;
        row.BranchId = sale.BranchId;
        row.FinancialDate = sale.FinancialDate;
        row.AccountId = sale.AccountId;
        row.AccountName = sale.AccountName;
        row.TotalAmount = sale.TotalAmount;
        row.ItemCount = sale.ItemCount;
        row.OrderPayloadJson = sale.OrderPayloadJson;
        row.PaymentLinesJson = sale.PaymentLinesJson;
        row.Status = (int)OfflineSaleStatus.PendingSync;
        row.IsOnlineVisibilityConfirmed = false;
        row.LastErrorMessage = null;
        row.NextAttemptAtUtc = null;
    }

    private static OfflineCashSale ToDomain(OfflineCashSaleEntity row) => OfflineCashSale.Restore(
        row.LocalId, row.LocalDisplayCode, row.BranchId, row.FinancialDate,
        row.AccountId, row.AccountName, row.TotalAmount, row.ItemCount,
        row.OrderPayloadJson, row.PaymentLinesJson,
        (OfflineSaleStatus)row.Status,
        row.CreatedAtUtc, row.SyncedAtUtc, row.ServerDocumentId,
        row.ServerDisplayCode, row.IsOnlineVisibilityConfirmed,
        row.LastErrorMessage, row.RetryCount,
        row.LastAttemptAtUtc, row.NextAttemptAtUtc);
}
