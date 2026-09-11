using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using System.Diagnostics;
using EBI.DM;
using SenangRetails.Shared.Services.CashSalesService;

namespace SenangRetails.Shared.Services.DashboardService
{
    public class DashboardService : IDashboardService
    {
        private readonly DashboardAC _dashboardAC;
        private readonly ICashSalesService _cashSalesService;

        public DashboardService(DashboardAC dashboardAC, ICashSalesService cashSalesService)
        {
            _dashboardAC = dashboardAC;
            _cashSalesService = cashSalesService;
        }

        /// <summary>
        /// Returns active member count and the 5 most recently created members in one API call.
        /// </summary>
        public async Task<(int ActiveCount, List<CustomerDM> Recent)> GetMemberDashboardDataAsync()
        {
            var all = await _dashboardAC.GetAllCustomersAsync();

            var recent = all
                .OrderByDescending(c => c.CreatedDateTime)
                .Take(2)
                .ToList();

            Debug.WriteLine($"[Dashboard] Total members: {all.Count}, Recent: {recent.Count}");
            return (all.Count, recent);
        }

        public async Task<decimal> GetSalesTodayAsync(string branchId)
        {
            var sales = await _cashSalesService.GetSalesHistoryAsync(branchId, DateTime.Today);
            var total = sales.Sum(sale => sale.TotalAfterTax);
            Debug.WriteLine($"[Dashboard] Sales today: {total}");
            return total;
        }

        public async Task<(decimal TotalSales, int BillsCount, decimal AvgSpending)> GetSalesSummaryTodayAsync(string branchId)
        {
            var sales = await _cashSalesService.GetSalesHistoryAsync(branchId, DateTime.Today);
            var totalSales = sales.Sum(sale => sale.TotalAfterTax);
            var billsCount = sales.Count;
            var avgSpending = billsCount > 0 ? totalSales / billsCount : 0m;

            Debug.WriteLine(
                $"[Dashboard] Today summary from merged sales: {billsCount} bill(s), total {totalSales}.");
            return (totalSales, billsCount, avgSpending);
        }

    }
}
