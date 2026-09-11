using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.CashDrawerService
{
    public interface ICashDrawerService
    {
        Task<IReadOnlyList<CashDrawerLogModel>> GetLogsAsync(
            string branchId,
            string counter,
            DateTime? businessDate = null,
            CancellationToken cancellationToken = default);

        Task<CashDrawerSummaryModel> GetSummaryAsync(
            string branchId,
            string counter,
            DateTime businessDate,
            CancellationToken cancellationToken = default);

        Task<CashDrawerOperationResult> RecordTransactionAsync(
            CashDrawerLogModel transaction,
            CancellationToken cancellationToken = default);
    }
}
