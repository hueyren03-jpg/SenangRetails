using EBI.DM;
using EBI.Enum;
using EBI.UC;
using Microsoft.JSInterop;
using System.Text.Json;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.InventoryService
{
    public class InventoryService : IInventoryService
    {
        private readonly InventoryAC _ac;
        private readonly AppState _appState;
        private readonly IJSRuntime _js;
        private readonly INetworkStatusService _network;
        private readonly ILocalCatalogRepository _catalogRepository;
        public InventoryService(
            InventoryAC ac,
            AppState appState,
            IJSRuntime JS,
            INetworkStatusService network,
            ILocalCatalogRepository catalogRepository)
        {
            _ac = ac;
            _appState = appState;
            _js = JS;
            _network = network;
            _catalogRepository = catalogRepository;
        }

        public async Task<Dictionary<string, List<InventoryDM>>?> GetCurrentBranchGroupedItemsAsync()
        {
            string branchId = _appState.SelectedBranchID;
            if (string.IsNullOrEmpty(branchId))
            {
                try
                {
                    branchId = await _js.InvokeAsync<string>("localStorage.getItem", "currentBranch");
                    if (!string.IsNullOrEmpty(branchId))
                        _appState.SelectedBranchID = branchId;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from localStorage: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(branchId))
                return null;

            return await LoadGroupedItemsAsync(branchId);
        }

        public async Task<List<InventoryDM>?> LoadItemsAsync(string branchId = "")
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadProxyAsync(branchId).ConfigureAwait(false);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        var items = response.result;
                        using var semaphore = new System.Threading.SemaphoreSlim(5);
                        var packageTasks = items
                            .Where(i => i.InventoryTypeID == 5 && (i.lstPackage == null || i.lstPackage.Count == 0))
                            .Select(async i =>
                        {
                            await semaphore.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                var fullPkg = await _ac.LoadFullAsync(i.MasterAccountID).ConfigureAwait(false);
                                if (fullPkg?.objInventory != null) i.lstPackage = fullPkg.objInventory.lstPackage;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[InventoryService] Package load failed: {ex.Message}");
                            }
                            finally { semaphore.Release(); }
                        });
                        await Task.WhenAll(packageTasks).ConfigureAwait(false);
                        await SaveItemsToSqliteAsync(items, branchId).ConfigureAwait(false);
                        return items;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Online load failed: {ex.Message}");
                }
            }

            return await LoadItemsFromSqliteAsync(branchId);
        }

        private async Task<List<InventoryDM>> LoadItemsFromSqliteAsync(string branchId)
        {
            try
            {
                var cached = await _catalogRepository.GetAsync(branchId).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(cached?.ItemsJson))
                    return new();

                return await Task.Run(
                    () => JsonSerializer.Deserialize<List<InventoryDM>>(cached.ItemsJson) ?? new())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] SQLite load failed: {ex.Message}");
                return new();
            }
        }

        private async Task SaveItemsToSqliteAsync(List<InventoryDM> items, string branchId)
        {
            try
            {
                var key = string.IsNullOrWhiteSpace(branchId) ? "default" : branchId;
                var itemsJson = await Task.Run(() => JsonSerializer.Serialize(items)).ConfigureAwait(false);
                await _catalogRepository.UpsertAsync(key, itemsJson: itemsJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] SQLite save failed: {ex.Message}");
            }
        }

        public async Task<InventoryDM?> LoadItemAsync(string masterAccountId)
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadRecordAsync(masterAccountId).ConfigureAwait(false);
                    if (response?.statusCode == 200) return response.result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Online item load failed: {ex.Message}");
                }
            }
            return (await LoadItemsFromSqliteAsync(_appState.SelectedBranchID).ConfigureAwait(false))
                .FirstOrDefault(x => x.MasterAccountID == masterAccountId);
        }

        public async Task<(bool Success, string Message)> CreateItemAsync(Inventory model)
        {
            model.objInventory.SaveAction = EBI.Enum.EntityState.Added;
            model.objInventory.IsDirty = true;
            var response = await _ac.CreateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Item created successfully.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] CreateItem failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<(bool Success, string Message, string? NewId)> CreatePackageAsync(Inventory model)
        {
            model.objInventory.IsDirty = true;
            var response = await _ac.CreateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.", null);
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Package created successfully.", response.result?.Id);
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] CreatePackage failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg, null);
        }

        public async Task<(bool Success, string Message, string? NewId)> SavePackageFullAsync(InventoryFullCreateRequest request)
        {
            var response = await _ac.CreatePackageFullAsync(request);
            if (response == null)
                return (false, "No response — check connection and login.", null);
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Saved.", response.result?.Id);
            return (false, !string.IsNullOrWhiteSpace(response.message) ? response.message : $"Server returned {response.statusCode}.", null);
        }

        public async Task<(bool Success, string Message)> UpdatePackageFullAsync(InventoryFullCreateRequest request)
        {
            var response = await _ac.UpdatePackageFullAsync(request);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Updated.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message) ? response.message : $"Server returned {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdatePackageFull failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<(bool Success, string Message)> UpdateProductFullAsync(InventoryFullCreateRequest request)
        {
            var response = await _ac.UpdateProductFullAsync(request);
            if (response == null)
                return (false, "The product update could not be confirmed. Check your connection and reopen the product before retrying.");
            if (response.statusCode is not (>= 200 and < 300))
                return (false, response.message ?? $"Server returned {response.statusCode}.");

            // Do not immediately reload the full aggregate after a successful update.
            // InventoryFull/LoadRecord is expensive and the editor already owns the saved values.
            return (true, !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : "Product updated successfully.");
        }

        public async Task<(bool Success, string Message)> UpdateItemAsync(Inventory model)
        {
            model.objInventory.SaveAction = EBI.Enum.EntityState.Changed;
            model.objInventory.IsDirty = true;
            var response = await _ac.UpdateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Item updated successfully.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdateItem failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<(bool success, string message)> UpdateInventoryFieldsAsync(string masterAccountId, string remarks, string? unitOfMeasureID = null, string? unitOfMeasureName = null, decimal? pointToRedeem = null, bool? allowPointRedemption = null, decimal? stockReorderLevel = null, string? itemDivisionID = null, string? itemDivisionName = null, string? itemDepartmentID = null, string? itemDepartmentName = null, string? itemCategoryID = null, string? itemCategoryName = null, string? itemSubCategoryID = null, string? itemSubCategoryName = null, string? brandName = null)
        {
            var resp = await _ac.UpdateClassificationFullAsync(
                masterAccountId,
                itemDivisionID,
                itemDivisionName,
                itemDepartmentID,
                itemDepartmentName,
                itemCategoryID,
                itemCategoryName,
                itemSubCategoryID,
                itemSubCategoryName,
                brandName,
                remarks,
                unitOfMeasureID,
                unitOfMeasureName,
                pointToRedeem,
                allowPointRedemption,
                stockReorderLevel);
            if (resp == null)
                return (false, "No response while saving product classification.");

            if (resp.statusCode >= 200 && resp.statusCode < 300)
            {
                return (true, !string.IsNullOrWhiteSpace(resp.message)
                    ? resp.message
                    : "Product classification updated successfully.");
            }

            return (false, resp.message ?? $"Server returned {resp.statusCode} while saving product classification.");
        }

        public async Task<(bool Success, string Message)> UpdateInventoryDirectAsync(InventoryCreateModel model)
        {
            model.saveAction = "Changed";
            model.isDirty = true;
            var response = await _ac.UpdateDirectAsync(model);
            if (response == null) return (false, "No response.");
            if (response.statusCode >= 200 && response.statusCode < 300) return (true, "");
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdateInventoryDirect failed: {response.statusCode} {response.message}");
            return (false, response.message ?? $"Server returned {response.statusCode}.");
        }

        public async Task<(bool Success, string Message)> DeleteItemAsync(string masterAccountId)
        {
            var response = await _ac.DeleteAsync(masterAccountId);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Item deleted successfully.");
            return (false, response?.message ?? "Failed to delete item.");
        }

        public async Task<Inventory?> LoadFullPackageAsync(string masterAccountId)
        {
            return await _ac.LoadFullAsync(masterAccountId);
        }

        public async Task<InventoryFullLoadDetail?> LoadFullPackageDetailAsync(string masterAccountId)
        {
            return await _ac.LoadFullDetailAsync(masterAccountId);
        }

        public async Task<Dictionary<string, List<InventoryDM>>?> LoadGroupedItemsAsync(string branchId = "")
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadProxyByItemGroupAsync(branchId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        await SaveItemsToSqliteAsync(response.result.Values.SelectMany(x => x).ToList(), branchId);
                        return response.result;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Grouped load failed: {ex.Message}");
                }
            }

            var items = await LoadItemsFromSqliteAsync(branchId);
            return items.GroupBy(x => string.IsNullOrWhiteSpace(x.ItemGroupName) ? "Uncategorized" : x.ItemGroupName)
                .ToDictionary(x => x.Key, x => x.ToList());
        }

        public async Task<List<rpt_StockBelowReorderPointDM>?> GetStockBelowReorderPointAsync()
        {
            var response = await _ac.GetStockBelowReorderPoint();
            if (response?.statusCode == 200)
                return response.result;
            return null;
        }

        public async Task<Dictionary<string, rpt_StockBalanceByBranchByItemsDM>?> GetStockBalanceByBranchAndByItemAsync(StockBalanceRequest request)
        {
            var response = await _ac.GetStockBalanceByBranchAndByItem(request);
            if (response?.statusCode == 200)
                return response.result;
            return null;
        }

        public async Task<bool> CreateStockInGRN(Doc_Stock_GRN request)
        {
            var response = await _ac.DocStockGRNCreateRecord(request);
            return response?.statusCode is >= 200 and < 300;
        }

        public async Task<Doc_Stock_GRN?> GetStockGRNRecordAsync(string documentId)
        {
            var response = await _ac.DocStockGRNLoadRecord(documentId);
            return response?.statusCode == 200 ? response.result : null;
        }

        public async Task<List<Doc_Stock_GRNDM>?> GetStockGRNRecordsAsync(
            string branchId,
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize)
        {
            var response = await _ac.DocStockGRNLoadProxy(branchId, startDate, endDate, pageNumber, pageSize);
            return response?.statusCode == 200 ? response.result : null;
        }

        public Task<ApiResponseRoot<string>?> DeleteStockGRNAsync(string documentId)
        {
            return _ac.DocStockGRNDeleteRecord(documentId);
        }

        public async Task<(bool Success, string Message, string? DocumentId)> CreateStockGINAsync(Doc_Stock_GIN request)
        {
            var response = await _ac.DocStockGINCreateRecord(request);
            if (response == null)
                return (false, "No response from the GIN API.", null);

            if (response.statusCode is >= 200 and < 300)
                return (true,
                    string.IsNullOrWhiteSpace(response.message) ? "GIN created successfully." : response.message,
                    response.result?.Id);

            return (false,
                string.IsNullOrWhiteSpace(response.message) ? $"GIN API returned {response.statusCode}." : response.message,
                null);
        }

        public async Task<(bool Success, string Message)> UpdateStockGINAsync(Doc_Stock_GIN request)
        {
            var response = await _ac.DocStockGINUpdateRecord(request);
            if (response == null)
                return (false, "No response from the GIN API.");

            if (response.statusCode is >= 200 and < 300)
                return (true,
                    string.IsNullOrWhiteSpace(response.message) ? "GIN updated successfully." : response.message);

            return (false,
                string.IsNullOrWhiteSpace(response.message) ? $"GIN API returned {response.statusCode}." : response.message);
        }

        public async Task<Doc_Stock_GIN?> GetStockGINRecordAsync(string documentId)
        {
            var response = await _ac.DocStockGINLoadRecord(documentId);
            return response?.statusCode == 200 ? response.result : null;
        }

        public async Task<List<Doc_Stock_GINDM>?> GetStockGINRecordsAsync(
            string branchId,
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize)
        {
            var response = await _ac.DocStockGINLoadProxy(branchId, startDate, endDate, pageNumber, pageSize);
            return response?.statusCode == 200 ? response.result : null;
        }

        public Task<ApiResponseRoot<string>?> DeleteStockGINAsync(string documentId)
        {
            return _ac.DocStockGINDeleteRecord(documentId);
        }

        public async Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentByBranchIdAsync(string? branchId = null)
        {
            var response = await _ac.GetPendingAcceptDocumentByBranchId(branchId);
            if (response?.statusCode == 200)
            {
                return response.result;
            }
            return null;
        }

        public async Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentDetailsAsync(string documentId)
        {
            var response = await _ac.GetPendingAcceptDocumentDetails(documentId);
            if (response?.statusCode == 200)
            {
                return response.result;
            }
            return null;
        }

        public async Task<ApiResponseRoot<string>?> AcceptStockInAsync(string documentId)
        {
            return await _ac.AcceptStockIn(documentId);
        }

        public async Task<StockTransferSaveResult> CreateStockTransfer(Doc_StockTransfer request)
        {
            var response = await _ac.CreateStockTransferAsync(request);
            if (response == null)
            {
                return new StockTransferSaveResult
                {
                    Message = "No response from the Stock Transfer API."
                };
            }

            if (response.statusCode is not (>= 200 and < 300))
            {
                return new StockTransferSaveResult
                {
                    Message = string.IsNullOrWhiteSpace(response.message)
                        ? $"Stock Transfer API returned {response.statusCode}."
                        : response.message
                };
            }

            return new StockTransferSaveResult
            {
                TransferSaved = true,
                TransferDocumentId = response.result?.Id,
                Message = string.IsNullOrWhiteSpace(response.result?.SuccessMessage)
                    ? (string.IsNullOrWhiteSpace(response.message) ? "Stock transfer created successfully." : response.message)
                    : response.result.SuccessMessage
            };
        }

        public async Task<List<Doc_StockTransfer>?> GetAllStockTransferRecordAsync(
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize)
        {
            var response = await _ac.GetAllStockTransferRecordAsync(startDate, endDate, pageNumber, pageSize);
            if (response?.statusCode != 200 || response.result == null)
            {
                return null;
            }

            return response.result
                .Where(record => record != null && !record.IsVoid)
                .Select(record => new Doc_StockTransfer
                {
                    objDoc_StockTransfer = record
                })
                .ToList();
        }

        public async Task<StockTransferSaveResult> UpdateStockTransfer(Doc_StockTransfer request)
        {
            var response = await _ac.UpdateStockTransferAsync(request);
            if (response == null)
            {
                return new StockTransferSaveResult
                {
                    Message = "No response from the Stock Transfer API."
                };
            }

            if (response.statusCode is not (>= 200 and < 300))
            {
                return new StockTransferSaveResult
                {
                    Message = string.IsNullOrWhiteSpace(response.message)
                        ? $"Stock Transfer API returned {response.statusCode}."
                        : response.message
                };
            }

            var transferDocumentId = !string.IsNullOrWhiteSpace(request.objDoc_StockTransfer.DocumentID)
                ? request.objDoc_StockTransfer.DocumentID
                : response.result?.Id;

            return new StockTransferSaveResult
            {
                TransferSaved = true,
                TransferDocumentId = transferDocumentId,
                Message = string.IsNullOrWhiteSpace(response.result?.SuccessMessage)
                    ? (string.IsNullOrWhiteSpace(response.message) ? "Stock transfer updated successfully." : response.message)
                    : response.result.SuccessMessage
            };
        }

        public async Task<Doc_StockTransfer?> GetStockTransferRecordAsync(string documentId)
        {
            var response = await _ac.GetStockTransferRecordAsync(documentId);
            return response?.statusCode == 200 ? response.result : null;
        }

        public Task<ApiResponseRoot<string>?> DeleteStockTransferAsync(string documentId)
        {
            return _ac.DeleteStockTransferAsync(documentId);
        }
    }
}
