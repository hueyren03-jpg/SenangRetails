using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.PriceGroupService;

public sealed class PriceGroupService(ILocalJsonCache cache) : IPriceGroupService
{
    private const string StorageKey = "senang_price_groups_data";
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly List<PriceGroupModel> SeedGroups =
    [
        new()
        {
            Code = "PG-VIP",
            Name = "VIP Member Price Group",
            Price = 18.00m,
            MlmCommRate = 5.0m,
            MlmSalesTarget = 1000.0m,
            VisibleBranchIds = ["HQ", "B01"]
        },
        new()
        {
            Code = "PG-REG",
            Name = "Standard Retail Price Group",
            Price = 25.00m,
            MlmCommRate = 2.0m,
            MlmSalesTarget = 500.0m,
            VisibleBranchIds = ["HQ", "B01", "B02"]
        }
    ];

    public async Task<List<PriceGroupModel>> GetPriceGroupsAsync()
    {
        var stored = await cache.GetAsync<List<PriceGroupModel>>(StorageKey);
        if (stored is not null)
        {
            return stored.Select(Normalize).OrderBy(x => x.Code).ToList();
        }

        var seeds = SeedGroups.Select(Normalize).ToList();
        await cache.SetAsync(StorageKey, seeds);
        return seeds;
    }

    public async Task<PriceGroupModel?> GetPriceGroupAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var groups = await GetPriceGroupsAsync();
        return groups.FirstOrDefault(g => g.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PriceGroupModel?> GetPriceGroupByProductIdAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId)) return null;
        var id = productId.Trim();
        var groups = await GetPriceGroupsAsync();
        return groups.FirstOrDefault(g =>
            g.AppliedProductIds.Any(x => x.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<string?> GetAssignedPriceGroupCodeAsync(string productId) =>
        (await GetPriceGroupByProductIdAsync(productId))?.Code;

    public async Task<decimal?> GetEffectiveProductPriceAsync(
        string productId,
        decimal originalPrice,
        string? branchId = null)
    {
        var group = await GetPriceGroupByProductIdAsync(productId);
        if (group is not { Price: > 0 }) return originalPrice;

        var isVisible = group.VisibleBranchIds.Count == 0 ||
                        string.IsNullOrWhiteSpace(branchId) ||
                        group.VisibleBranchIds.Any(x =>
                            x.Equals(branchId.Trim(), StringComparison.OrdinalIgnoreCase));
        return isVisible ? group.Price : originalPrice;
    }

    public async Task<bool> SavePriceGroupAsync(PriceGroupModel group, bool isNew)
    {
        if (!TryNormalizeAndValidate(group, out var normalized)) return false;

        await _writeLock.WaitAsync();
        try
        {
            var groups = await GetPriceGroupsAsync();
            var index = groups.FindIndex(x =>
                x.Code.Equals(normalized.Code, StringComparison.OrdinalIgnoreCase));

            if (isNew && index >= 0) return false;

            var assignedIds = normalized.AppliedProductIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var other in groups.Where((_, i) => i != index))
            {
                other.AppliedProductIds.RemoveAll(assignedIds.Contains);
            }

            if (index >= 0)
            {
                normalized.CreatedAt = groups[index].CreatedAt;
                groups[index] = normalized;
            }
            else
            {
                normalized.CreatedAt = DateTime.UtcNow;
                groups.Add(normalized);
            }

            await cache.SetAsync(StorageKey, groups.OrderBy(x => x.Code).ToList());
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeletePriceGroupAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        await _writeLock.WaitAsync();
        try
        {
            var groups = await GetPriceGroupsAsync();
            var removed = groups.RemoveAll(x =>
                x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;

            await cache.SetAsync(StorageKey, groups);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> AssignProductToPriceGroupAsync(string productId, string? priceGroupCode)
    {
        if (string.IsNullOrWhiteSpace(productId)) return false;
        var id = productId.Trim();

        await _writeLock.WaitAsync();
        try
        {
            var groups = await GetPriceGroupsAsync();
            if (!string.IsNullOrWhiteSpace(priceGroupCode) &&
                !groups.Any(x => x.Code.Equals(priceGroupCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            foreach (var group in groups)
            {
                group.AppliedProductIds.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(priceGroupCode) &&
                    group.Code.Equals(priceGroupCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    group.AppliedProductIds.Add(id);
                }
            }

            await cache.SetAsync(StorageKey, groups);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static bool TryNormalizeAndValidate(PriceGroupModel? source, out PriceGroupModel normalized)
    {
        normalized = source is null ? new PriceGroupModel() : Normalize(source);
        return source is not null
            && !string.IsNullOrWhiteSpace(normalized.Code)
            && !string.IsNullOrWhiteSpace(normalized.Name)
            && source.Price >= 0
            && source.MlmCommRate is >= 0 and <= 100
            && source.MlmSalesTarget >= 0;
    }

    private static PriceGroupModel Normalize(PriceGroupModel source) => new()
    {
        Code = (source.Code ?? string.Empty).Trim().ToUpperInvariant(),
        Name = (source.Name ?? string.Empty).Trim(),
        Price = decimal.Round(Math.Max(0, source.Price), 2),
        MlmCommRate = decimal.Round(Math.Clamp(source.MlmCommRate, 0, 100), 2),
        MlmSalesTarget = decimal.Round(Math.Max(0, source.MlmSalesTarget), 2),
        VisibleBranchIds = NormalizeIds(source.VisibleBranchIds),
        AppliedProductIds = NormalizeIds(source.AppliedProductIds),
        CreatedAt = source.CreatedAt
    };

    private static List<string> NormalizeIds(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
