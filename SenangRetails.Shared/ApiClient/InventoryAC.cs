using EBI.DM;
using EBI.EF;
using EBI.UC;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using static SenangRetails.Shared.Pages.Home;
using InventoryFullRequest = SenangRetails.Shared.Models.DTOs.InventoryFullCreateRequest;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SenangRetails.Shared.ApiClient
{
    public class InventoryAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;
        private readonly AppState _appState;
        private readonly ConcurrentDictionary<string, string> _fullPayloadCache =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly JsonSerializerOptions PayloadJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public InventoryAC(IStoreTokenService tokenService, AppState appState) : base()
        {
            _tokenService = tokenService;
            _appState = appState;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        /// <summary>Load inventory items by branch. Pass empty string to load all.</summary>
        public async Task<ApiResponseRoot<List<InventoryDM>>?> LoadProxyAsync(string branchId = "")
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryDM>>>(
                "api/Inventory/LoadProxy", new { id = string.IsNullOrEmpty(branchId) ? null : branchId });
        }

        /// <summary>Load inventory items grouped by category for a specific branch.</summary>
        public async Task<ApiResponseRoot<Dictionary<string, List<InventoryDM>>>?> LoadProxyByItemGroupAsync(string branchId)
        {
            if (!await SetBearerToken()) return null;
            var payload = new { id = string.IsNullOrEmpty(branchId) ? null : branchId };
            return await PostAsync<object, ApiResponseRoot<Dictionary<string, List<InventoryDM>>>>(
                "api/Inventory/LoadProxyByItemGroup", payload);
        }

        /// <summary>Load the InventoryDM section of a single item.</summary>
        public async Task<ApiResponseRoot<InventoryDM>?> LoadRecordAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<InventoryDM>>(
                "api/Inventory/LoadRecord", new { id = masterAccountId });
        }

        public async Task<ApiResponseRoot<PackageSaveResult>?> CreateAsync(Inventory model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Inventory, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Create", model);
        }

        public async Task<ApiResponseRoot<object>?> UpdateAsync(Inventory model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Inventory, ApiResponseRoot<object>>(
                "api/InventoryFull/Update", model);
        }

        /// <summary>
        /// Updates classification values inside objInventory without replacing
        /// unrelated fields in the loaded inventory aggregate.
        /// </summary>
        public async Task<ApiResponseRoot<object>?> UpdateClassificationFullAsync(
            string masterAccountId,
            string? itemDivisionID,
            string? itemDivisionName,
            string? itemDepartmentID,
            string? itemDepartmentName,
            string? itemCategoryID,
            string? itemCategoryName,
            string? itemSubCategoryID,
            string? itemSubCategoryName,
            string? brandName,
            string? remarks = null,
            string? unitOfMeasureID = null,
            string? unitOfMeasureName = null,
            decimal? pointToRedeem = null,
            bool? allowPointRedemption = null,
            decimal? stockReorderLevel = null)
        {
            return await UpdateFieldsFullPayloadAsync(
                masterAccountId,
                remarks,
                unitOfMeasureID,
                unitOfMeasureName,
                pointToRedeem,
                allowPointRedemption,
                stockReorderLevel,
                itemDivisionID,
                itemDivisionName,
                itemDepartmentID,
                itemDepartmentName,
                itemCategoryID,
                itemCategoryName,
                itemSubCategoryID,
                itemSubCategoryName,
                brandName);
        }

        /// <summary>Load the full Inventory aggregate (including lstPackage, lstMembershipCredit) for a single item.</summary>
        public async Task<Inventory?> LoadFullAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            var response = await PostAsync<object, ApiResponseRoot<Inventory>>(
                "api/InventoryFull/LoadRecord", new { id = masterAccountId });
            return (response?.statusCode >= 200 && response.statusCode < 300) ? response.result : null;
        }

        /// <summary>
        /// Load the full Inventory aggregate using our own DTO (guaranteed public setters).
        /// Avoids EBI DLL deserialization issues where lstMembershipCredit may not be writable.
        /// </summary>
        public async Task<InventoryFullLoadDetail?> LoadFullDetailAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            var response = await PostAsync<object, ApiResponseRoot<JsonObject>>(
                "api/InventoryFull/LoadRecord", new { id = masterAccountId });
            System.Diagnostics.Debug.WriteLine($"[LoadFullDetailAsync] raw={LastPostResponseBody}");
            if (response?.statusCode is not (>= 200 and < 300) || response.result == null)
                return null;

            CacheFullPayload(masterAccountId, response.result);
            return JsonSerializer.Deserialize<InventoryFullLoadDetail>(
                response.result.ToJsonString(), PayloadJsonOptions);
        }

        /// <summary>
        /// Save a package/topup (insert OR update) with membership credits at root level.
        /// Both insert and update use api/InventoryFull/Create; objInventory.saveAction
        /// ("Added"/"Changed") tells the server whether to insert or update.
        /// </summary>
        public async Task<ApiResponseRoot<PackageSaveResult>?> CreatePackageFullAsync(InventoryFullRequest request)
        {
            if (!await SetBearerToken()) return null;
            System.Diagnostics.Debug.WriteLine($"[CreatePackageFullAsync] {System.Text.Json.JsonSerializer.Serialize(request)}");
            return await PostAsync<InventoryFullRequest, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Create", request);
        }

        /// <summary>
        /// Update a package/topup with membership credits at root level.
        /// Uses api/InventoryFull/Update (distinct from Create) so the server
        /// runs its update code path which processes lstMembershipCredit at root level.
        /// </summary>
        public async Task<ApiResponseRoot<PackageSaveResult>?> UpdatePackageFullAsync(InventoryFullRequest request)
        {
            if (!await SetBearerToken()) return null;
            System.Diagnostics.Debug.WriteLine($"[UpdatePackageFullAsync] {System.Text.Json.JsonSerializer.Serialize(request)}");
            return await PostAsync<InventoryFullRequest, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Update", request);
        }

        /// <summary>Update the editable product/service fields in one full-inventory request.</summary>
        public async Task<ApiResponseRoot<object>?> UpdateProductFullAsync(InventoryFullRequest request)
        {
            var model = request.objInventory;
            if (model.hasPackage)
                throw new ArgumentException("Use the package update operation for package items.", nameof(request));

            var hasSkuChanges = model.lstSKU is not null;
            JsonObject? payload;
            JsonObject? inventory;
            if (hasSkuChanges)
            {
                payload = await LoadFullPayloadAsync(model.masterAccountID);
                inventory = payload == null ? null : GetProperty(payload, "objInventory") as JsonObject;
            }
            else
            {
                inventory = await LoadInventoryPayloadAsync(model.masterAccountID);
                payload = inventory == null
                    ? null
                    : new JsonObject { ["objInventory"] = inventory.DeepClone() };
            }

            if (payload == null || inventory == null)
                return null;

            var edited = JsonSerializer.SerializeToNode(model)!.AsObject();

            // Only overlay fields owned by the product editor; retain prices, flags,
            // collections and other server fields that the form does not expose.
            string[] fields =
            [
                "inventoryTypeID", "accountName", "itemGroupID", "itemGroupName",
                "itemDivisionID", "itemDivisionName", "itemDepartmentID", "itemDepartmentName",
                "itemCategoryID", "itemCategoryName", "itemSubCategoryID", "itemSubCategoryName", "brandName",
                "salesDescription", "displayCode", "vendorItemCode", "salesPrice", "purchasePrice",
                "taxCodeID", "isTaxInclusive", "isSold", "accountStatus", "unitOfMeasureID",
                "unitOfMeasureName", "uomBase", "hasUOM", "stockReorderLevel", "branchID", "hasPackage", "validityDays",
                "remarks", "staffCommissionA", "staffCommissionB", "staffCommissionC",
                "allowPointRedemption"
            ];
            foreach (var field in fields)
                SetProperty(inventory, field, edited[field]?.DeepClone());

            if (model.lstSKU is not null)
            {
                SetProperty(inventory, "lstSKU", edited["lstSKU"]?.DeepClone());

                var validationError = ValidateAndNormalizeSkuUpdatePayload(inventory, model.masterAccountID);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return new ApiResponseRoot<object>
                    {
                        statusCode = 400,
                        message = validationError
                    };
                }
            }
            else
            {

                RemoveCaseVariants(inventory, "lstSKU");
                RemoveChildCollections(inventory);
                RemoveCaseVariants(payload, "lstSKU");
            }

            // The documented API property is a string, not a JSON number.
            SetProperty(inventory, "pointToRedeem", JsonValue.Create(model.PointToRedeem?.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            SetProperty(inventory, "saveAction", JsonValue.Create("Changed"));
            SetProperty(inventory, "isDirty", JsonValue.Create(true));
            RemoveSubGroups(inventory);
            SetProperty(payload, "lstMasterAccount_Branch", JsonSerializer.SerializeToNode(request.lstMasterAccount_Branch));

            // lstSKU is an aggregate child collection. Inventory/Update accepts the parent model
            // but does not persist its SKU rows, so selling units must use InventoryFull/Update.
            var response = await PostRawJsonAsync<ApiResponseRoot<object>>(
                "api/InventoryFull/Update", payload.ToJsonString());
            if (response?.statusCode is >= 200 and < 300 && !string.IsNullOrWhiteSpace(model.masterAccountID))
            {
                // Do not cache the submitted request as a server-confirmed aggregate. Reload it
                // when the editor next opens so ignored child rows cannot appear falsely saved.
                _fullPayloadCache.TryRemove(model.masterAccountID, out _);

                if (hasSkuChanges)
                {
                    var reloadedPayload = await LoadFullPayloadAsync(model.masterAccountID);
                    if (reloadedPayload == null)
                    {
                        return new ApiResponseRoot<object>
                        {
                            statusCode = 502,
                            message = "The product update returned Success, but the saved selling units could not be verified from InventoryFull/LoadRecord. The UOM draft has been kept; retry after the API is available."
                        };
                    }

                    if (!SkuChangesWerePersisted(reloadedPayload, model.lstSKU!))
                    {
                        _fullPayloadCache.TryRemove(model.masterAccountID, out _);
                        return new ApiResponseRoot<object>
                        {
                            statusCode = 409,
                            message = "The API returned Success but did not retain the submitted lstSKU selling units. The UOM draft has been kept."
                        };
                    }
                }
            }
            return response;
        }

        private static string? ValidateAndNormalizeSkuUpdatePayload(JsonObject inventory, string? masterAccountId)
        {
            if (string.IsNullOrWhiteSpace(masterAccountId))
                return "Selling units cannot be saved before the product has a Master Account ID.";

            if (GetProperty(inventory, "lstSKU") is not JsonArray skuRows)
                return "The selling-unit request does not contain a valid lstSKU collection.";

            foreach (var node in skuRows)
            {
                if (node is not JsonObject row)
                    return "The selling-unit request contains an invalid lstSKU row.";

                var skuName = GetProperty(row, "skuName")?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(skuName))
                    return "Every selling unit must have a UOM name.";

                var autoId = GetProperty(row, "autoID")?.GetValue<string>();
                var saveAction = GetProperty(row, "saveAction")?.GetValue<string>();
                var isNew = string.IsNullOrWhiteSpace(autoId) ||
                    string.Equals(saveAction, "Added", StringComparison.OrdinalIgnoreCase);

                if (isNew)
                {
                    // Match the POS insert contract exactly. The aggregate operation assigns
                    // both identifiers after the new SKU row is created.
                    SetProperty(row, "isLoading", JsonValue.Create(true));
                    SetProperty(row, "autoID", JsonValue.Create(string.Empty));
                    SetProperty(row, "inventoryAccountID", JsonValue.Create(string.Empty));
                    SetProperty(row, "purchasePrice", JsonValue.Create(0m));
                    var barcode = GetProperty(row, "barcode")?.GetValue<string>();
                    SetProperty(row, "barcode", JsonValue.Create(barcode?.Trim() ?? string.Empty));
                    SetProperty(row, "saveAction", JsonValue.Create("Added"));
                    SetProperty(row, "isDirty", JsonValue.Create(true));
                }
                else
                {
                    SetProperty(row, "isLoading", JsonValue.Create(false));
                    if (string.IsNullOrWhiteSpace(GetProperty(row, "inventoryAccountID")?.GetValue<string>()))
                        SetProperty(row, "inventoryAccountID", JsonValue.Create(masterAccountId));

                    if (GetProperty(row, "barcode") is null)
                        SetProperty(row, "barcode", JsonValue.Create(string.Empty));
                }
            }

            return null;
        }

        private static bool SkuChangesWerePersisted(
            JsonObject payload,
            IReadOnlyCollection<InventorySkuEntry> submittedRows)
        {
            if (GetProperty(payload, "objInventory") is not JsonObject inventory ||
                GetProperty(inventory, "lstSKU") is not JsonArray savedNodes)
            {
                return false;
            }

            var savedRows = savedNodes
                .OfType<JsonObject>()
                .Select(node => node.Deserialize<InventorySkuEntry>(PayloadJsonOptions))
                .Where(row => row != null)
                .Cast<InventorySkuEntry>()
                .ToList();

            foreach (var submitted in submittedRows)
            {
                var isDeleted = string.Equals(submitted.saveAction, "Deleted", StringComparison.OrdinalIgnoreCase);
                var saved = savedRows.FirstOrDefault(row =>
                    (!string.IsNullOrWhiteSpace(submitted.autoID) &&
                     string.Equals(row.autoID, submitted.autoID, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(row.skuName?.Trim(), submitted.skuName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (isDeleted)
                {
                    if (saved != null)
                        return false;
                    continue;
                }

                if (saved == null ||
                    (!string.IsNullOrWhiteSpace(submitted.inventoryAccountID) &&
                     !string.Equals(saved.inventoryAccountID, submitted.inventoryAccountID, StringComparison.OrdinalIgnoreCase)) ||
                    !string.Equals(saved.skuName?.Trim(), submitted.skuName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    saved.skuQuantity != submitted.skuQuantity ||
                    saved.salesPrice != submitted.salesPrice ||
                    saved.purchasePrice != submitted.purchasePrice ||
                    !string.Equals(saved.barcode?.Trim(), submitted.barcode?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<JsonObject?> LoadInventoryPayloadAsync(string? masterAccountId)
        {
            if (string.IsNullOrWhiteSpace(masterAccountId))
                return null;

            if (_fullPayloadCache.TryGetValue(masterAccountId, out var cachedJson) &&
                JsonNode.Parse(cachedJson) is JsonObject cachedPayload &&
                GetProperty(cachedPayload, "objInventory") is JsonObject cachedInventory)
            {
                return cachedInventory.DeepClone().AsObject();
            }

            if (!await SetBearerToken())
                return null;

            var response = await PostAsync<object, ApiResponseRoot<JsonObject>>(
                "api/Inventory/LoadRecord", new { id = masterAccountId });
            return response?.statusCode is >= 200 and < 300
                ? response.result
                : null;
        }

        private async Task<JsonObject?> LoadFullPayloadAsync(string? masterAccountId)
        {
            if (string.IsNullOrWhiteSpace(masterAccountId))
                return null;

            if (_fullPayloadCache.TryGetValue(masterAccountId, out var cachedJson) &&
                JsonNode.Parse(cachedJson) is JsonObject cachedPayload)
            {
                return cachedPayload;
            }

            if (!await SetBearerToken())
                return null;

            // LastPostResponseBody is shared diagnostic state, not this request's result.
            var response = await PostAsync<object, ApiResponseRoot<JsonObject>>(
                "api/InventoryFull/LoadRecord", new { id = masterAccountId });
            if (response?.statusCode is not (>= 200 and < 300) || response.result == null)
                return null;
            var inventory = GetProperty(response.result, "objInventory") as JsonObject;
            if (inventory == null || !string.Equals(GetProperty(inventory, "masterAccountID")?.GetValue<string>(), masterAccountId, StringComparison.Ordinal))
                return null;
            CacheFullPayload(masterAccountId, response.result);
            return response.result;
        }

        private void CacheFullPayload(string masterAccountId, JsonObject payload)
        {
            if (!string.IsNullOrWhiteSpace(masterAccountId))
                _fullPayloadCache[masterAccountId] = payload.ToJsonString();
        }

        private static JsonNode? GetProperty(JsonObject target, string name) =>
            target.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

        private static void RemoveCaseVariants(JsonObject target, string name)
        {
            foreach (var key in target.Select(p => p.Key).Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)).ToArray())
                target.Remove(key);
        }

        private static void SetProperty(JsonObject target, string name, JsonNode? value)
        {
            RemoveCaseVariants(target, name);
            target[name] = value;
        }

        private static void RemoveSubGroups(JsonObject inventory)
        {
            foreach (var name in new[] { "subGroup1", "subGroup2", "subGroup3", "subGroup4" })
                RemoveCaseVariants(inventory, name);
        }

        private static void RemoveChildCollections(JsonObject inventory)
        {
            foreach (var key in inventory
                .Select(property => property.Key)
                .Where(key => key.StartsWith("lst", StringComparison.OrdinalIgnoreCase))
                .ToArray())
            {
                inventory.Remove(key);
            }
        }

        /// <summary>
        /// Update inventory record via the simple api/Inventory/Update endpoint.
        /// This endpoint updates all InventoryDM columns directly (including Remarks),
        /// used as a belt-and-suspenders call after InventoryFull/Update for Product/Service.
        /// </summary>
        public async Task<ApiResponseRoot<object>?> UpdateDirectAsync(InventoryCreateModel model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<InventoryCreateModel, ApiResponseRoot<object>>("api/Inventory/Update", model);
        }

        /// <summary>
        /// Load the full Inventory aggregate, patch its nested objInventory values,
        /// and submit it through api/InventoryFull/Update.
        /// </summary>
        public async Task<ApiResponseRoot<object>?> UpdateFieldsFullPayloadAsync(
            string masterAccountId,
            string? remarks,
            string? unitOfMeasureID,
            string? unitOfMeasureName,
            decimal? pointToRedeem = null,
            bool? allowPointRedemption = null,
            decimal? stockReorderLevel = null,
            string? itemDivisionID = null,
            string? itemDivisionName = null,
            string? itemDepartmentID = null,
            string? itemDepartmentName = null,
            string? itemCategoryID = null,
            string? itemCategoryName = null,
            string? itemSubCategoryID = null,
            string? itemSubCategoryName = null,
            string? brandName = null)
        {
            var node = await LoadFullPayloadAsync(masterAccountId);
            if (node == null)
                return null;
            var inventory = GetProperty(node, "objInventory")!.AsObject();

            static void SetString(System.Text.Json.Nodes.JsonObject target, string propertyName, string? value)
            {
                RemoveCaseVariants(target, propertyName);
                target[propertyName] = value;
            }

            static void SetDecimal(System.Text.Json.Nodes.JsonObject target, string propertyName, decimal value)
            {
                RemoveCaseVariants(target, propertyName);
                target[propertyName] = value;
            }

            static void SetBoolean(System.Text.Json.Nodes.JsonObject target, string propertyName, bool value)
            {
                RemoveCaseVariants(target, propertyName);
                target[propertyName] = value;
            }

            SetString(inventory, "remarks", remarks);
            if (unitOfMeasureID != null) SetString(inventory, "unitOfMeasureID", unitOfMeasureID);
            if (unitOfMeasureName != null) SetString(inventory, "unitOfMeasureName", unitOfMeasureName);
            if (pointToRedeem != null) SetDecimal(inventory, "pointToRedeem", pointToRedeem.Value);
            if (allowPointRedemption != null) SetBoolean(inventory, "allowPointRedemption", allowPointRedemption.Value);
            if (stockReorderLevel != null) SetDecimal(inventory, "stockReorderLevel", stockReorderLevel.Value);

            SetString(inventory, "itemDivisionID", itemDivisionID);
            SetString(inventory, "itemDivisionName", itemDivisionName);
            SetString(inventory, "itemDepartmentID", itemDepartmentID);
            SetString(inventory, "itemDepartmentName", itemDepartmentName);
            SetString(inventory, "itemCategoryID", itemCategoryID);
            SetString(inventory, "itemCategoryName", itemCategoryName);
            SetString(inventory, "itemSubCategoryID", itemSubCategoryID);
            SetString(inventory, "itemSubCategoryName", itemSubCategoryName);
            SetString(inventory, "brandName", brandName);

            RemoveCaseVariants(inventory, "subGroup1");
            RemoveCaseVariants(inventory, "subGroup2");
            RemoveCaseVariants(inventory, "subGroup3");
            RemoveCaseVariants(inventory, "subGroup4");

            // ItemAppCategory is unrelated to SupportingTable types 55 and 56.
            // Preserve its existing server value without using it as a fallback.
            SetString(inventory, "saveAction", "Changed");
            SetBoolean(inventory, "isDirty", true);

            // This operation changes product metadata only. Existing SKU rows returned by
            // LoadRecord must not be resubmitted through the broken SKU save procedure.
            RemoveCaseVariants(inventory, "lstSKU");
            RemoveCaseVariants(node, "lstSKU");

            System.Diagnostics.Debug.WriteLine($"[UpdateFieldsFullPayloadAsync] InventoryFull payload={node.ToJsonString()}");
            var response = await PostRawJsonAsync<ApiResponseRoot<object>>("api/InventoryFull/Update", node.ToJsonString());
            if (response?.statusCode is >= 200 and < 300)
                CacheFullPayload(masterAccountId, node);
            return response;
        }

        /// <summary>Delete an inventory item by its MasterAccountID.</summary>
        public async Task<ApiResponseRoot<object>?> DeleteAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            return await DeleteAsync<ApiResponseRoot<object>>(
                $"api/InventoryFull/Delete?id={Uri.EscapeDataString(masterAccountId)}");
        }

        public async Task<ApiResponseRoot<List<Models.DTOs.LowStockItem>>?> GetStockBelowReorderPoint()
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<Models.DTOs.LowStockItem>>>(
                "api/Inventory/GetStockBelowReorderPoint", new { id = _appState.CurrentBranch?.BranchID });
        }

        public async Task<ApiResponseRoot<Dictionary<string, StockBalanceItem>>?> GetStockBalanceByBranchAndByItem(StockBalanceRequest request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<StockBalanceRequest, ApiResponseRoot<Dictionary<string, StockBalanceItem>>>(
                    "api/Inventory/GetStockBalanceByBranchAndByItem", request);
        }

        public async Task<ApiResponseRoot<string>> DocStockGRNCreateRecord(EBI.UC.Doc_Stock_GRN request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<EBI.UC.Doc_Stock_GRN, ApiResponseRoot<string>>(
                    "api/Doc_Stock_GRN/CreateRecord", request);
        }

        // Goods Issue Note (GIN) API — mirrors the GRN integration pattern.
        public async Task<ApiResponseRoot<string>?> DocStockGINCreateRecord(GinDocumentDto request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<GinDocumentDto, ApiResponseRoot<string>>(
                "api/Doc_Stock_GIN/CreateRecord", request);
        }

        public async Task<ApiResponseRoot<string>?> DocStockGINUpdateRecord(GinDocumentDto request)
        {
            if (!await SetBearerToken()) return null;
            return await PutAsync<GinDocumentDto, ApiResponseRoot<string>>(
                "api/Doc_Stock_GIN/UpdateRecord", request);
        }

        public async Task<ApiResponseRoot<GinDocumentDto>?> DocStockGINLoadRecord(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<GinDocumentDto>>(
                "api/Doc_Stock_GIN/LoadRecord", new { id = documentId });
        }

        public async Task<ApiResponseRoot<List<Doc_Stock_GINDM>>?> DocStockGINLoadProxy(
            string branchId,
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<Doc_Stock_GINDM>>>(
                "api/Doc_Stock_GIN/LoadProxy",
                new
                {
                    branchID = branchId,
                    startDate,
                    endDate,
                    pageNumber,
                    pageSize
                });
        }

        public async Task<ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>?> GetPendingAcceptDocumentByBranchId(string? branchId = null)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>>(
                    "api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentByBranchID",
                    new { id = string.IsNullOrWhiteSpace(branchId) ? _appState.CurrentBranch?.BranchID : branchId }
                );
        }

        public async Task<ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>> GetPendingAcceptDocumentDetails(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>>(
                    "api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentDetails", new { id = documentId }
                );
        }

        public async Task<ApiResponseRoot<string>> AcceptStockIn(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<string>>(
                    "api/InventoryMovement_PendingAccept/AcceptStockIn", new { id = documentId }
                );
        }

        public async Task<ApiResponseRoot<string>> CreateStockTransferAsync(Doc_StockTransfer request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Doc_StockTransfer, ApiResponseRoot<string>>(
                "api/Doc_StockTransfer/CreateRecord", request);
        }

        public async Task<ApiResponseRoot<List<Doc_StockTransferDM>>> GetAllStockTransferRecordAsync(
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<Doc_StockTransferDM>>>(
                "api/Doc_StockTransfer/LoadProxy",
                new
                {
                    branchId = _appState.CurrentBranch?.BranchID,
                    startDate,
                    endDate,
                    pageNumber,
                    pageSize
                });
        }

        public async Task<ApiResponseRoot<string>> UpdateStockTransferAsync(Doc_StockTransfer request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Doc_StockTransfer, ApiResponseRoot<string>>(
                "api/Doc_StockTransfer/UpdateRecord", request);
        }

        public async Task<ApiResponseRoot<Doc_StockTransfer>?> GetStockTransferRecordAsync(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<Doc_StockTransfer>>(
                "api/Doc_StockTransfer/LoadRecord", new { id = documentId });
        }

        public async Task<ApiResponseRoot<string>> DeleteStockTransferAsync(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<string>>(
                "api/Doc_StockTransfer/Delete", new { id = documentId });
        }
    }
}
