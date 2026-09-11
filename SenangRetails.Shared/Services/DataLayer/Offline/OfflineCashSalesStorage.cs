using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.UC;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Domain.Sales;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.DataLayer.Offline
{
    public class OfflineCashSalesStorage : IOfflineCashSalesStorage
    {
        private readonly IOfflineCashSaleRepository _repository;
        public OfflineCashSalesStorage(IOfflineCashSaleRepository repository)
        {
            _repository = repository;
        }

        public async Task<OfflineCashSaleEntity> SaveOfflineSaleAsync(Doc_CashSales order, List<PaymentLine> payments)
        {
            var localId = string.IsNullOrWhiteSpace(order.objDoc_CashSales?.DocumentID)
                ? Guid.NewGuid().ToString()
                : order.objDoc_CashSales.DocumentID;
            if (order.objDoc_CashSales != null)
                order.objDoc_CashSales.DocumentID = localId;
            var localDisplayCode = $"OFF-{DateTime.Now:yyyyMMdd}-{localId[..Math.Min(8, localId.Length)].ToUpperInvariant()}";

            decimal total = 0;
            int itemCount = 0;

            if (order.lstDocumentLine != null)
            {
                var activeLines = order.lstDocumentLine
                    .Where(line => line.SaveAction != EBI.Enum.EntityState.Deleted)
                    .ToList();
                itemCount = activeLines.Count;
                total = activeLines.Sum(line => line.UnitPrice * line.Quantity - line.Discount);
            }
            if (total == 0 && order.objDoc_CashSales != null)
            {
                total = order.objDoc_CashSales.TotalAfterTax != 0 ? order.objDoc_CashSales.TotalAfterTax : order.objDoc_CashSales.TotalBeforeTax;
            }
            if (total == 0 && payments != null)
            {
                total = payments.Sum(p => p.Amount);
            }

            var branchId = order.objDoc_CashSales?.BranchID ?? "";
            var finDate = order.objDoc_CashSales != null && order.objDoc_CashSales.FinancialDate != default 
                ? order.objDoc_CashSales.FinancialDate 
                : DateTime.Today;
            var accountId = order.objDoc_CashSales?.AccountID ?? "";
            var accountName = string.IsNullOrWhiteSpace(order.objDoc_CashSales?.AccountName) 
                ? "Walk-In Customer" 
                : order.objDoc_CashSales.AccountName;

            var orderJson = JsonSerializer.Serialize(order);
            var paymentsJson = JsonSerializer.Serialize(payments ?? new List<PaymentLine>());
            var entity = new OfflineCashSaleEntity
            {
                LocalId = localId,
                LocalDisplayCode = localDisplayCode,
                BranchId = branchId,
                FinancialDate = finDate,
                AccountId = accountId,
                AccountName = accountName,
                TotalAmount = total,
                ItemCount = itemCount,
                OrderPayloadJson = orderJson,
                PaymentLinesJson = paymentsJson,
                Status = SyncStatus.PendingSync,
                CreatedAtUtc = DateTime.UtcNow,
                RetryCount = 0
            };

            await _repository.AddAsync(OfflineCashSale.Restore(
                entity.LocalId,
                entity.LocalDisplayCode,
                entity.BranchId,
                entity.FinancialDate,
                entity.AccountId,
                entity.AccountName,
                entity.TotalAmount,
                entity.ItemCount,
                entity.OrderPayloadJson,
                entity.PaymentLinesJson,
                OfflineSaleStatus.PendingSync,
                entity.CreatedAtUtc,
                null,
                null,
                null,
                false,
                null,
                0,
                null,
                null));

            return entity;
        }

        public async Task<List<OfflineCashSaleEntity>> GetPendingOfflineSalesAsync(bool ignoreSchedule = false)
        {
            var sales = await _repository.GetPendingAsync(DateTime.UtcNow, ignoreSchedule);
            return sales.Select(ToLegacyEntity).ToList();
        }

        public async Task<List<OfflineCashSaleEntity>> GetAllLocalSalesAsync(string branchId, DateTime date)
        {
            var sales = await _repository.GetForDateAsync(branchId, date);
            return sales.Select(ToLegacyEntity).ToList();
        }

        public async Task<bool> MarkSaleSyncedAsync(string localId, string serverDocId, string serverDisplayCode)
        {
            return await _repository.MarkSyncedAsync(localId, serverDocId, serverDisplayCode);
        }

        public Task<bool> MarkSaleAttemptStartedAsync(string localId, DateTime attemptedAtUtc)
        {
            return _repository.MarkAttemptStartedAsync(localId, attemptedAtUtc);
        }

        public Task<bool> ScheduleSaleRetryAsync(string localId, string errorMessage, DateTime nextAttemptAtUtc)
        {
            return _repository.ScheduleRetryAsync(localId, errorMessage, nextAttemptAtUtc);
        }

        public Task<bool> MarkSaleRequiresReviewAsync(string localId, string errorMessage)
        {
            return _repository.MarkRequiresReviewAsync(localId, errorMessage);
        }

        public async Task<int> GetPendingCountAsync()
        {
            return await _repository.GetPendingCountAsync();
        }

        public async Task<OfflineCashSaleEntity?> GetSaleByLocalIdAsync(string localId)
        {
            var sale = await _repository.GetByLocalIdAsync(localId);
            return sale is null ? null : ToLegacyEntity(sale);
        }

        public async Task<bool> DeleteLocalSaleAsync(string localId)
        {
            return await _repository.DeleteAsync(localId);
        }

        private static OfflineCashSaleEntity ToLegacyEntity(OfflineCashSale sale) => new()
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
            Status = sale.Status switch
            {
                OfflineSaleStatus.Synced => SyncStatus.Synced,
                OfflineSaleStatus.RetryScheduled => SyncStatus.Failed,
                OfflineSaleStatus.RequiresReview => SyncStatus.Conflict,
                _ => SyncStatus.PendingSync
            },
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
    }
}
