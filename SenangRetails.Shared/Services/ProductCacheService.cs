using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.DM;
using EBI.Enum;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services
{
    /// <summary>
    /// Hybrid in-memory and SQLite cache for inventory items and categories.
    /// Loads immediately from memory or local SQLite when offline.
    /// Refreshes silently in the background when online.
    /// </summary>
    public class ProductCacheService
    {
        private readonly IInventoryService _inventoryService;
        private readonly ISupportingTableService _supportingTableService;
        private readonly ILocalCatalogRepository _catalogRepository;

        private const string DefaultCatalogKey = "default";
        private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(2);
        private bool _isRefreshing;
        private bool _isRefreshingSellingUnits;
        private string _itemsBranchKey = string.Empty;
        private DateTime _lastSuccessfulRefreshAtUtc = DateTime.MinValue;
        private readonly Dictionary<string, InventoryDM> _localPatches = new();
        private readonly Dictionary<string, string> _imageCache = new();

        public List<InventoryDM>? Items { get; private set; }
        public List<SupportingTableItem>? Categories { get; private set; }
        public bool HasCache => Items != null && Items.Count > 0;
        public bool HasCacheForBranch(string branchId) =>
            HasCache && _itemsBranchKey == NormalizeCatalogKey(branchId);

        /// <summary>Raised on a background thread when a silent refresh completes.</summary>
        public event Action? OnCacheUpdated;

        public ProductCacheService(
            IInventoryService inventoryService,
            ISupportingTableService supportingTableService,
            ILocalCatalogRepository catalogRepository)
        {
            _inventoryService = inventoryService;
            _supportingTableService = supportingTableService;
            _catalogRepository = catalogRepository;
        }

        /// <summary>
        /// Ensures items are loaded, first checking memory, then local SQLite, then online API.
        /// </summary>
        public async Task<List<InventoryDM>?> EnsureLoadedAsync(string branchId = "")
        {
            var key = NormalizeCatalogKey(branchId);
            if (Items != null && Items.Count > 0 && _itemsBranchKey == key)
            {
                return Items;
            }

            // 1. Try loading from SQLite (Instant offline load)
            try
            {
                var cached = await _catalogRepository.GetAsync(branchId).ConfigureAwait(false);

                if (cached != null && !string.IsNullOrWhiteSpace(cached.ItemsJson))
                {
                    var sqliteItems = await Task.Run(
                        () => JsonSerializer.Deserialize<List<InventoryDM>>(cached.ItemsJson))
                        .ConfigureAwait(false);
                    if (sqliteItems != null && sqliteItems.Count > 0)
                    {
                        ApplyLocalPatches(sqliteItems);
                        Items = sqliteItems;
                        _itemsBranchKey = key;
                        if (!string.IsNullOrWhiteSpace(cached.CategoriesJson))
                        {
                            var cats = await Task.Run(
                                () => JsonSerializer.Deserialize<List<SupportingTableItem>>(cached.CategoriesJson))
                                .ConfigureAwait(false);
                            if (cats != null) Categories = cats;
                        }

                        Console.WriteLine($"[ProductCacheService] Loaded {Items.Count} items from SQLite offline cache.");
                        return Items;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] SQLite load error: {ex.Message}");
            }

            // 2. Fallback to online API fetch if SQLite is empty
            try
            {
                var onlineItems = await _inventoryService.LoadItemsAsync(branchId).ConfigureAwait(false);

                if (onlineItems != null && onlineItems.Count > 0)
                {
                    SetItems(onlineItems, branchId);
                    await SaveItemsToSqliteAsync(onlineItems, branchId).ConfigureAwait(false);
                    _lastSuccessfulRefreshAtUtc = DateTime.UtcNow;
                    return onlineItems;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] Online fetch error: {ex.Message}");
            }

            return _itemsBranchKey == key ? Items : null;
        }

        /// <summary>Store fresh items in memory for one catalog branch.</summary>
        public void SetItems(List<InventoryDM> items, string branchId = "")
        {
            var key = NormalizeCatalogKey(branchId);
            ApplyLocalPatches(items);
            Items = items;
            _itemsBranchKey = key;
            _lastSuccessfulRefreshAtUtc = DateTime.UtcNow;
        }

        public void PatchCachedItem(string masterAccountId, Action<InventoryDM> patch)
        {
            if (Items == null) return;
            var item = Items.FirstOrDefault(x => x.MasterAccountID == masterAccountId);
            if (item == null) return;
            patch(item);
            _localPatches[masterAccountId] = item;

            _ = SaveItemsToSqliteAsync(Items, _itemsBranchKey);
        }

        public IReadOnlyList<Inventory_SKUDM> GetSellingUnits(InventoryDM? product)
        {
            if (product?.lstSKU == null) return Array.Empty<Inventory_SKUDM>();
            return product.lstSKU
                .Where(row => row.SaveAction != EntityState.Deleted && row.SKUQuantity > 0)
                .OrderBy(row => row.SKUQuantity)
                .ThenBy(row => row.SKUName)
                .ToList();
        }

        public void SetSellingUnits(string? productId, IEnumerable<Inventory_SKUDM> units)
        {
            if (string.IsNullOrWhiteSpace(productId) || Items == null) return;
            var product = Items.FirstOrDefault(item =>
                string.Equals(item.MasterAccountID, productId, StringComparison.OrdinalIgnoreCase));
            if (product == null) return;

            var normalized = units
                .Where(row => row.SaveAction != EntityState.Deleted && row.SKUQuantity > 0)
                .OrderBy(row => row.SKUQuantity)
                .ToList();
            product.lstSKU = new ObservableCollection<Inventory_SKUDM>(normalized);
            product.HasUOM = normalized.Count > 0;
            product.UOMBase = 1;
            _localPatches[productId] = product;
            _ = SaveItemsToSqliteAsync(Items, _itemsBranchKey);
        }

        public async Task<IReadOnlyList<Inventory_SKUDM>> GetOrLoadSellingUnitsAsync(InventoryDM product)
        {
            var cached = GetSellingUnits(product);
            if (cached.Count > 0 || !product.HasUOM || string.IsNullOrWhiteSpace(product.MasterAccountID))
                return cached;

            try
            {
                var full = await _inventoryService.LoadFullPackageDetailAsync(product.MasterAccountID).ConfigureAwait(false);
                var source = full?.objInventory?.lstSKU?.Any() == true
                    ? full.objInventory.lstSKU
                    : full?.lstSKU;
                if (source == null) return cached;

                var runtimeRows = source
                    .Where(row => !string.Equals(row.saveAction, "Deleted", StringComparison.OrdinalIgnoreCase))
                    .Select(row => ToRuntimeSku(row, product.MasterAccountID))
                    .ToList();
                SetSellingUnits(product.MasterAccountID, runtimeRows);
                product.lstSKU = new ObservableCollection<Inventory_SKUDM>(runtimeRows);
                product.HasUOM = runtimeRows.Count > 0;
                product.UOMBase = 1;
                return runtimeRows;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] Selling unit load error for '{product.MasterAccountID}': {ex.Message}");
                return cached;
            }
        }

        public (InventoryDM Product, Inventory_SKUDM Sku)? FindSellingUnitByBarcode(string barcode)
        {
            if (Items == null || string.IsNullOrWhiteSpace(barcode)) return null;
            var value = barcode.Trim();
            foreach (var product in Items)
            {
                var sku = GetSellingUnits(product).FirstOrDefault(row =>
                    string.Equals(row.Barcode?.Trim(), value, StringComparison.OrdinalIgnoreCase));
                if (sku != null) return (product, sku);
            }
            return null;
        }

        public void TriggerSellingUnitRefresh()
        {
            if (_isRefreshingSellingUnits || Items == null) return;
            _ = RefreshSellingUnitsAsync();
        }

        public void RemoveLocalPatch(string masterAccountId)
        {
            _localPatches.Remove(masterAccountId);
        }

        public void ClearLocalPatches()
        {
            _localPatches.Clear();
        }

        public void StoreImage(string masterAccountId, string base64DataUri)
        {
            if (!string.IsNullOrEmpty(masterAccountId) && !string.IsNullOrEmpty(base64DataUri))
                _imageCache[masterAccountId] = base64DataUri;
        }

        public string? GetImage(string masterAccountId)
        {
            if (string.IsNullOrEmpty(masterAccountId)) return null;
            return _imageCache.TryGetValue(masterAccountId, out var v) ? v : null;
        }

        public void RemoveImage(string masterAccountId)
        {
            if (!string.IsNullOrEmpty(masterAccountId))
                _imageCache.Remove(masterAccountId);
        }

        /// <summary>
        /// Resolves a full display image URL from either base64 cache or backend server URL.
        /// </summary>
        public string ResolveImageUrl(InventoryDM? item)
        {
            if (item == null) return string.Empty;

            var masterId = item.MasterAccountID?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(masterId))
            {
                var cached = GetImage(masterId);
                if (!string.IsNullOrEmpty(cached)) return cached;
            }

            return ResolveServerImageUrl(item.ImagePath, item.ImageFileName);
        }

        /// <summary>
        /// Constructs a full absolute URL for server-hosted product images, handling relative paths and backslashes.
        /// </summary>
        public static string ResolveServerImageUrl(string? imagePath, string? imageFileName)
        {
            string rawPath = imagePath?.Trim() ?? string.Empty;
            string rawFile = imageFileName?.Trim() ?? string.Empty;

            string rawUrl = string.Empty;
            if (!string.IsNullOrEmpty(rawPath) && !string.IsNullOrEmpty(rawFile))
            {
                string separator = (rawPath.EndsWith("/") || rawPath.EndsWith("\\")) ? "" : "/";
                rawUrl = $"{rawPath}{separator}{rawFile}";
            }
            else if (!string.IsNullOrEmpty(rawPath))
            {
                rawUrl = rawPath;
            }
            else if (!string.IsNullOrEmpty(rawFile))
            {
                rawUrl = rawFile;
            }

            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

            rawUrl = rawUrl.Replace("\\", "/");

            if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return rawUrl;
            }

            const string apiBase = "https://ebisoftware.com.my:5000/";
            string cleanRelative = rawUrl.StartsWith("/") ? rawUrl.Substring(1) : rawUrl;
            return $"{apiBase}{cleanRelative}";
        }

        public void SetCategories(List<SupportingTableItem> categories, string branchId = "")
        {
            Categories = categories;

            _ = SaveCategoriesToSqliteAsync(categories, ResolveRefreshBranchId(branchId));
        }

        public void ClearAll()
        {
            Items = null;
            Categories = null;
            _localPatches.Clear();
            _itemsBranchKey = string.Empty;
            _lastSuccessfulRefreshAtUtc = DateTime.MinValue;
        }

        private async Task RefreshSellingUnitsAsync()
        {
            if (_isRefreshingSellingUnits || Items == null) return;
            _isRefreshingSellingUnits = true;
            try
            {
                foreach (var product in Items.Where(item => item.HasUOM && GetSellingUnits(item).Count == 0).ToList())
                    await GetOrLoadSellingUnitsAsync(product).ConfigureAwait(false);
            }
            finally
            {
                _isRefreshingSellingUnits = false;
            }
        }

        private static Inventory_SKUDM ToRuntimeSku(InventorySkuEntry row, string productId) => new()
        {
            IsLoading = false,
            AutoID = row.autoID ?? string.Empty,
            InventoryAccountID = row.inventoryAccountID ?? productId,
            SKUName = row.skuName,
            SKUQuantity = row.skuQuantity,
            SalesPrice = row.salesPrice,
            PurchasePrice = row.purchasePrice,
            Barcode = row.barcode ?? string.Empty,
            SaveAction = string.IsNullOrWhiteSpace(row.autoID) ? EntityState.Added : EntityState.Changed,
            IsDirty = false
        };

        public void TriggerBackgroundRefresh(string branchId = "")
        {
            if (_isRefreshing) return;

            var resolvedBranchId = ResolveRefreshBranchId(branchId);
            var key = NormalizeCatalogKey(resolvedBranchId);
            if (_itemsBranchKey == key &&
                DateTime.UtcNow - _lastSuccessfulRefreshAtUtc < BackgroundRefreshInterval)
            {
                return;
            }

            _ = DoBackgroundRefreshAsync(resolvedBranchId);
        }

        public async Task RefreshAsync(string branchId)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                var key = NormalizeCatalogKey(branchId);
                var itemsTask = _inventoryService.LoadItemsAsync(branchId);
                var categoriesTask = _supportingTableService.LoadListByTypeAsync(4);
                await Task.WhenAll(itemsTask, categoriesTask).ConfigureAwait(false);

                var items = await itemsTask.ConfigureAwait(false);
                var categories = await categoriesTask.ConfigureAwait(false);
                if (items != null && items.Count > 0)
                {
                    ApplyLocalPatches(items);
                    Items = items;
                    _itemsBranchKey = key;
                    _lastSuccessfulRefreshAtUtc = DateTime.UtcNow;
                    await SaveItemsToSqliteAsync(items, branchId).ConfigureAwait(false);
                }
                if (categories != null)
                    Categories = categories;

                if (categories != null)
                    await SaveCategoriesToSqliteAsync(categories, branchId).ConfigureAwait(false);

                OnCacheUpdated?.Invoke();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task DoBackgroundRefreshAsync(string branchId)
        {
            try
            {
                await RefreshAsync(branchId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] Background refresh error: {ex.Message}");
            }
        }

        private void ApplyLocalPatches(List<InventoryDM> list)
        {
            if (_localPatches.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i].MasterAccountID;
                if (id != null && _localPatches.TryGetValue(id, out var patch))
                    list[i] = patch;
            }
        }

        private async Task SaveItemsToSqliteAsync(List<InventoryDM> items, string branchId)
        {
            if (items == null || items.Count == 0) return;

            try
            {
                var key = NormalizeCatalogKey(branchId);
                var itemsJson = await Task.Run(() => JsonSerializer.Serialize(items)).ConfigureAwait(false);
                await _catalogRepository.UpsertAsync(
                    key,
                    itemsJson: itemsJson).ConfigureAwait(false);

                Console.WriteLine($"[ProductCacheService] Saved {items.Count} patched items to SQLite for branch '{key}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] SQLite save error: {ex.Message}");
            }
        }

        private async Task SaveCategoriesToSqliteAsync(List<SupportingTableItem> categories, string branchId)
        {
            try
            {
                var key = NormalizeCatalogKey(branchId);
                var categoriesJson = await Task.Run(() => JsonSerializer.Serialize(categories)).ConfigureAwait(false);
                await _catalogRepository.UpsertAsync(key, categoriesJson: categoriesJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] Category cache save error: {ex.Message}");
            }
        }

        private string ResolveRefreshBranchId(string branchId)
        {
            if (!string.IsNullOrWhiteSpace(branchId))
                return branchId.Trim();

            return string.IsNullOrWhiteSpace(_itemsBranchKey) || _itemsBranchKey == DefaultCatalogKey
                ? string.Empty
                : _itemsBranchKey;
        }

        private static string NormalizeCatalogKey(string branchId) =>
            string.IsNullOrWhiteSpace(branchId) ? DefaultCatalogKey : branchId.Trim();
    }
}
