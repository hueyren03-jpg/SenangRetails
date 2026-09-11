using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EBI.UC;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.DataLayer.Offline
{
    public interface IOfflineCashSalesStorage
    {
        Task<OfflineCashSaleEntity> SaveOfflineSaleAsync(Doc_CashSales order, List<PaymentLine> payments);
        Task<List<OfflineCashSaleEntity>> GetPendingOfflineSalesAsync(bool ignoreSchedule = false);
        Task<List<OfflineCashSaleEntity>> GetAllLocalSalesAsync(string branchId, DateTime date);
        Task<bool> MarkSaleSyncedAsync(string localId, string serverDocId, string serverDisplayCode);
        Task<bool> MarkSaleAttemptStartedAsync(string localId, DateTime attemptedAtUtc);
        Task<bool> ScheduleSaleRetryAsync(string localId, string errorMessage, DateTime nextAttemptAtUtc);
        Task<bool> MarkSaleRequiresReviewAsync(string localId, string errorMessage);
        Task<int> GetPendingCountAsync();
        Task<OfflineCashSaleEntity?> GetSaleByLocalIdAsync(string localId);
        Task<bool> DeleteLocalSaleAsync(string localId);
    }
}
