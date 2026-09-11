using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.DM;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private readonly POSPaymentLineTypeAC _ac;
        private readonly AppState _appState;
        private readonly INetworkStatusService _network;
        private readonly ILocalCatalogRepository _catalogRepository;

        public PaymentService(
            POSPaymentLineTypeAC ac,
            AppState appState,
            INetworkStatusService network,
            ILocalCatalogRepository catalogRepository)
        {
            _ac = ac;
            _appState = appState;
            _network = network;
            _catalogRepository = catalogRepository;
        }

        public async Task<List<Doc_CashSales_POSPaymentLineTypeDM>> GetPaymentMethodsAsync(string branchId, string groupId, string customerId, bool includeInactive = false)
        {
            List<Doc_CashSales_POSPaymentLineTypeDM>? rawList = null;

            // 1. Attempt online fetch
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var requestPayload = new POSPaymentLineTypeRequest
                    {
                        ID = string.IsNullOrEmpty(customerId) ? null : customerId,
                        BranchID = string.IsNullOrEmpty(branchId) ? null : branchId,
                        GroupID = string.IsNullOrEmpty(groupId) ? null : groupId
                    };

                    var response = await _ac.GetSystemControlledSalesSettlementTypeAsync(requestPayload);

                    if (response?.statusCode == 200 && response.result != null && response.result.Count > 0)
                    {
                        rawList = response.result;
                        await SavePaymentMethodsToSqliteAsync(rawList, branchId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PaymentService] Online fetch failed: {ex.Message}. Falling back to SQLite cache.");
                }
            }

            // 2. Fallback to SQLite if online fetch failed or returned null
            if (rawList == null || rawList.Count == 0)
            {
                rawList = await LoadPaymentMethodsFromSqliteAsync(branchId);
            }

            // 3. Fallback to default POS payment methods if SQLite is also empty
            if (rawList == null || rawList.Count == 0)
            {
                rawList = GetDefaultOfflinePaymentMethods(groupId);
            }

            // 4. Apply filtering by group and customer
            var targetGroupId = groupId;
            var filteredList = rawList.Where(m =>
            {
                bool isVisibleInGroup = string.IsNullOrEmpty(targetGroupId) ||
                                        string.IsNullOrEmpty(m.VisibleInGroup) ||
                                        m.VisibleInGroup.Split(',')
                                            .Select(x => x.Trim())
                                            .Contains(targetGroupId, StringComparer.OrdinalIgnoreCase);

                if (m.POSPaymentTypeID == -1)
                {
                    if (_appState.SelectedCustomer == null || string.IsNullOrEmpty(_appState.SelectedCustomer.MasterAccountID))
                    {
                        return false;
                    }
                }

                return (includeInactive || m.Active) && isVisibleInGroup;
            }).ToList();

            if (!_network.IsInternetAvailable)
            {
                filteredList = filteredList
                    .Where(method => method.POSPaymentTypeName.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filteredList.Count == 0)
                {
                    filteredList.Add(new Doc_CashSales_POSPaymentLineTypeDM
                    {
                        POSPaymentTypeID = 1,
                        POSPaymentTypeName = "Cash",
                        Active = true,
                        VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                    });
                }
            }

            return filteredList;
        }

        private async Task<List<Doc_CashSales_POSPaymentLineTypeDM>?> LoadPaymentMethodsFromSqliteAsync(string branchId)
        {
            try
            {
                var cached = await _catalogRepository.GetAsync(branchId);

                if (cached != null && !string.IsNullOrWhiteSpace(cached.PaymentMethodsJson))
                {
                    var list = JsonSerializer.Deserialize<List<Doc_CashSales_POSPaymentLineTypeDM>>(cached.PaymentMethodsJson);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentService] SQLite load error: {ex.Message}");
            }

            return null;
        }

        private async Task SavePaymentMethodsToSqliteAsync(List<Doc_CashSales_POSPaymentLineTypeDM> methods, string branchId)
        {
            if (methods == null || methods.Count == 0) return;

            try
            {
                var key = string.IsNullOrEmpty(branchId) ? "default" : branchId;
                await _catalogRepository.UpsertAsync(
                    key,
                    paymentMethodsJson: JsonSerializer.Serialize(methods));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentService] SQLite save error: {ex.Message}");
            }
        }

        private List<Doc_CashSales_POSPaymentLineTypeDM> GetDefaultOfflinePaymentMethods(string groupId)
        {
            return
            [
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 1,
                    POSPaymentTypeName = "Cash",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 2,
                    POSPaymentTypeName = "Credit Card",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 3,
                    POSPaymentTypeName = "Debit Card",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 4,
                    POSPaymentTypeName = "E-Wallet / QR Pay",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 5,
                    POSPaymentTypeName = "Online Transfer",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                }
            ];
        }
    }
}
