using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Services.InventoryService;

namespace SenangRetails.Shared.Services.PromotionSetupService;

public sealed class PromotionSetupService(
    ILocalJsonCache cache,
    IInventoryService inventoryService) : IPromotionSetupService
{
    private const string StorageKey = "senang_promotion_rules";
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string? LastError { get; private set; }

    public async Task<List<PromotionSetupModel>> GetPromotionsAsync()
    {
        try
        {
            var records = await LoadApiPromotionRecordsAsync();
            var promotions = records
                .Select(record => MapApiPromotion(record.Header, record.Aggregate))
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Code)
                .ToList();

            await cache.SetAsync(StorageKey, promotions);
            LastError = null;
            return promotions;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            var stored = await cache.GetAsync<List<PromotionSetupModel>>(StorageKey);
            return stored?.Select(Normalize).OrderBy(item => item.Name).ToList() ?? [];
        }
    }

    public async Task<PromotionSetupModel?> GetPromotionAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var promotions = await GetPromotionsAsync();
        return promotions.FirstOrDefault(x =>
            x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SavePromotionAsync(PromotionSetupModel promo, bool isNew)
    {
        LastError = null;
        if (!TryNormalizeAndValidate(promo, out var normalized, out var validationError))
        {
            LastError = validationError;
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            var promotions = await GetPromotionsAsync();
            var index = promotions.FindIndex(x =>
                x.Code.Equals(normalized.Code, StringComparison.OrdinalIgnoreCase));

            if (isNew && index >= 0)
            {
                LastError = $"Promotion code {normalized.Code} already exists.";
                return false;
            }

            var assignedIds = normalized.AppliedProductIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var other in promotions.Where((_, i) => i != index))
            {
                other.AppliedProductIds.RemoveAll(assignedIds.Contains);
            }

            if (index >= 0)
            {
                promotions[index] = normalized;
            }
            else
            {
                promotions.Add(normalized);
            }

            await cache.SetAsync(StorageKey, promotions.OrderBy(x => x.Code).ToList());
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeletePromotionAsync(string code)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            LastError = "Promotion code is required.";
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            var promotions = await GetPromotionsAsync();
            var removed = promotions.RemoveAll(x =>
                x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                LastError = "Promotion was not found.";
                return false;
            }

            await cache.SetAsync(StorageKey, promotions);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string?> GetAssignedPromotionCodeAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId)) return null;
        var id = productId.Trim();
        var promotions = await GetPromotionsAsync();
        return promotions.FirstOrDefault(x =>
            x.AppliedProductIds.Any(y => y.Equals(id, StringComparison.OrdinalIgnoreCase)))?.Code;
    }

    public async Task<bool> AssignProductToPromotionAsync(string productId, string? promoCode)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(productId))
        {
            LastError = "Product ID is required.";
            return false;
        }

        var id = productId.Trim();
        var selectedCode = promoCode?.Trim() ?? string.Empty;
        await _writeLock.WaitAsync();
        try
        {
            var records = await LoadApiPromotionRecordsAsync();
            if (!string.IsNullOrWhiteSpace(selectedCode) &&
                !records.Any(record => PromotionMatches(record.Header, selectedCode)))
            {
                LastError = "Promotion was not found.";
                return false;
            }

            foreach (var record in records)
            {
                var promotion = MapApiPromotion(record.Header, record.Aggregate);
                var shouldContainProduct = !string.IsNullOrWhiteSpace(selectedCode) &&
                    PromotionMatches(record.Header, selectedCode);
                var containsProduct = promotion.AppliedProductIds.Contains(id, StringComparer.OrdinalIgnoreCase);
                if (containsProduct == shouldContainProduct)
                    continue;

                if (shouldContainProduct)
                    promotion.AppliedProductIds.Add(id);
                else
                    promotion.AppliedProductIds.RemoveAll(value =>
                        value.Equals(id, StringComparison.OrdinalIgnoreCase));

                if (!ApplyProductAssignments(record.Aggregate, promotion.AppliedProductIds))
                {
                    LastError = $"Promotion {promotion.Name} has no active package rule to update.";
                    return false;
                }

                var result = await inventoryService.UpdateItemAsync(record.Aggregate);
                if (!result.Success)
                {
                    LastError = string.IsNullOrWhiteSpace(result.Message)
                        ? $"Promotion {promotion.Name} could not be updated."
                        : result.Message;
                    return false;
                }
            }

            var refreshed = records
                .Select(record => MapApiPromotion(record.Header, record.Aggregate))
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Code)
                .ToList();
            await cache.SetAsync(StorageKey, refreshed);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<List<ApiPromotionRecord>> LoadApiPromotionRecordsAsync()
    {
        var inventory = await inventoryService.LoadItemsAsync("")
            ?? throw new InvalidOperationException("The Inventory API did not return promotion data.");
        var headers = inventory
            .Where(item => item.InventoryTypeID == 8 && !string.IsNullOrWhiteSpace(item.MasterAccountID))
            .GroupBy(item => item.MasterAccountID, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var records = new List<ApiPromotionRecord>(headers.Count);

        foreach (var header in headers)
        {
            var aggregate = await inventoryService.LoadFullPackageAsync(header.MasterAccountID);
            if (aggregate == null)
                continue;

            records.Add(new ApiPromotionRecord(aggregate.objInventory ?? header, aggregate));
        }

        return records;
    }

    private static PromotionSetupModel MapApiPromotion(
        InventoryDM header,
        EBI.UC.Inventory aggregate)
    {
        var rules = (aggregate.objInventory?.lstPackage ?? header.lstPackage ?? [])
            .Where(rule => !rule.IsVoided)
            .ToList();
        var rule = rules.FirstOrDefault(item => item.PromotionMethod != 0) ?? rules.FirstOrDefault();
        var appliedProductIds = rules
            .SelectMany(GetRuleAppliedProductIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var source = aggregate.objInventory ?? header;

        return new PromotionSetupModel
        {
            MasterAccountID = source.MasterAccountID ?? header.MasterAccountID ?? string.Empty,
            Code = source.DisplayCode ?? header.DisplayCode ?? string.Empty,
            Name = source.AccountName ?? header.AccountName ?? string.Empty,
            PromoMethod = GetPromotionMethodName(rule?.PromotionMethod ?? 0),
            PromoType = rule?.PromotionMethod == 1 ? "Fixed Price" : "Percentage",
            DiscountValue = ResolvePromotionValue(rule),
            StartDate = source.AvailableDateFrom.Year > 1 ? source.AvailableDateFrom : null,
            EndDate = source.AvailableDateTo.Year > 1 ? source.AvailableDateTo : null,
            AvailableTimeFrom = source.AvailableTimeFrom,
            AvailableTimeTo = source.AvailableTimeTo,
            MinQuantity = Math.Max(1, Convert.ToInt32(rule?.Quantity ?? 1)),
            MaxLimitPerOrder = Math.Max(0, Convert.ToInt32(rule?.MaxQuantity ?? 0)),
            IsActive = source.IsSold,
            Remarks = source.Remarks ?? string.Empty,
            BranchID = source.BranchID ?? string.Empty,
            PromoPriority = source.PromoPriority,
            MaxDiscountLimit = source.MaxDiscountLimit,
            PromoConditionAmount = rule?.PromoConditionAmt ?? 0,
            AppliedProductIds = appliedProductIds,
            OptionItems = string.Join(",", appliedProductIds)
        };
    }

    private static IReadOnlyList<string> GetRuleAppliedProductIds(Inventory_PackageItemDM rule)
    {
        var optionItems = ParseOptionValues(rule.OptionItems);
        if (optionItems.Count > 0)
            return optionItems;

        return string.IsNullOrWhiteSpace(rule.InventoryID)
            ? []
            : [rule.InventoryID.Trim()];
    }

    private static IReadOnlyList<string> ParseOptionValues(string? rawValue) =>
        string.IsNullOrWhiteSpace(rawValue)
            ? []
            : rawValue
                .Split([',', ';', '|', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim(' ', '[', ']', '"', '\''))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static bool ApplyProductAssignments(
        EBI.UC.Inventory aggregate,
        IEnumerable<string> productIds)
    {
        var rules = aggregate.objInventory?.lstPackage?
            .Where(rule => !rule.IsVoided)
            .ToList() ?? [];
        if (rules.Count == 0)
            return false;

        var normalizedIds = productIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var optionItems = string.Join(",", normalizedIds);
        var primaryProductId = normalizedIds.FirstOrDefault() ?? string.Empty;

        foreach (var rule in rules)
        {
            rule.OptionItems = optionItems;
            if (string.IsNullOrWhiteSpace(rule.InventoryID) ||
                !normalizedIds.Contains(rule.InventoryID.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                rule.InventoryID = primaryProductId;
            }

            rule.IsDirty = true;
            rule.SaveAction = string.IsNullOrWhiteSpace(rule.AutoID)
                ? EBI.Enum.EntityState.Added
                : EBI.Enum.EntityState.Changed;
        }

        aggregate.objInventory.HasPackage = true;
        aggregate.objInventory.IsDirty = true;
        aggregate.objInventory.SaveAction = EBI.Enum.EntityState.Changed;
        return true;
    }

    private static bool PromotionMatches(InventoryDM promotion, string value) =>
        string.Equals(promotion.DisplayCode, value, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(promotion.MasterAccountID, value, StringComparison.OrdinalIgnoreCase);

    private static string GetPromotionMethodName(int method) => method switch
    {
        1 => "Fixed Unit Price",
        2 => "Discount %",
        3 => "Discount Amt",
        4 => "Bundled Discount",
        5 => "Lump Sum Discount",
        6 => "Lump Sum Fixed Price",
        _ => "No Effect"
    };

    private static decimal ResolvePromotionValue(Inventory_PackageItemDM? rule)
    {
        if (rule == null)
            return 0;
        if (rule.UnitActualValue != 0)
            return rule.UnitActualValue;
        if (rule.PromotionMethod == 2 && rule.UnitPrice is > 0 and <= 1)
            return rule.UnitPrice * 100m;
        return rule.UnitPrice;
    }

    private sealed record ApiPromotionRecord(InventoryDM Header, EBI.UC.Inventory Aggregate);

    private static bool TryNormalizeAndValidate(
        PromotionSetupModel? source,
        out PromotionSetupModel normalized,
        out string? error)
    {
        normalized = source is null ? new PromotionSetupModel() : Normalize(source);
        error = null;

        if (string.IsNullOrWhiteSpace(normalized.Code)) error = "Promotion code is required.";
        else if (string.IsNullOrWhiteSpace(normalized.Name)) error = "Promotion name is required.";
        else if (normalized.StartDate.HasValue && normalized.EndDate.HasValue && normalized.StartDate > normalized.EndDate)
            error = "The end date must be on or after the start date.";
        else if (normalized.MinQuantity < 1) error = "Minimum quantity must be at least 1.";
        else if (normalized.MaxLimitPerOrder < 0) error = "Maximum limit cannot be negative.";
        else if (normalized.DiscountValue < 0) error = "Promotion value cannot be negative.";
        else if (normalized.PromoMethod == "Discount %" && normalized.DiscountValue > 100)
            error = "Percentage discount cannot exceed 100%.";

        return error is null;
    }

    private static PromotionSetupModel Normalize(PromotionSetupModel source)
    {
        var method = string.IsNullOrWhiteSpace(source.PromoMethod) ? "Discount %" : source.PromoMethod.Trim();
        var type = method switch
        {
            "Discount %" => "Percentage",
            "Discount Amt" => "Discount Amount",
            "Fixed Unit Price" => "Fixed Price",
            _ => method
        };

        return new PromotionSetupModel
        {
            Code = (source.Code ?? string.Empty).Trim().ToUpperInvariant(),
            Name = (source.Name ?? string.Empty).Trim(),
            PromoType = type,
            PromoMethod = method,
            Rounding = string.IsNullOrWhiteSpace(source.Rounding) ? "No Rounding" : source.Rounding.Trim(),
            PromoCondition = string.IsNullOrWhiteSpace(source.PromoCondition) ? "No Condition" : source.PromoCondition.Trim(),
            DiscountValue = decimal.Round(Math.Max(0, source.DiscountValue), 2),
            StartDate = source.StartDate?.Date,
            EndDate = source.EndDate?.Date,
            MinQuantity = Math.Max(1, source.MinQuantity),
            MaxLimitPerOrder = Math.Max(0, source.MaxLimitPerOrder),
            IsActive = source.IsActive,
            Remarks = (source.Remarks ?? string.Empty).Trim(),
            AppliedProductIds = (source.AppliedProductIds ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static List<PromotionSetupModel> GetDefaultSeedPromotions() =>
    [
        new()
        {
            Code = "PROMO-SAMPLE-10",
            Name = "Sample 10% Discount",
            PromoMethod = "Discount %",
            PromoType = "Percentage",
            DiscountValue = 10,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(1),
            IsActive = false,
            Remarks = "Mock rule. Assign products and activate when ready."
        },
        new()
        {
            Code = "PROMO-SAMPLE-FIXED",
            Name = "Sample Fixed Unit Price",
            PromoMethod = "Fixed Unit Price",
            PromoType = "Fixed Price",
            DiscountValue = 9.90m,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(1),
            IsActive = false,
            Remarks = "Mock rule. Assign products and activate when ready."
        }
    ];
}
