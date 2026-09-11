using System.Globalization;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.BarcodeSetupService;

public sealed class BarcodeSetupService(ILocalJsonCache cache) : IBarcodeSetupService
{
    private const string RulesStorageKey = "senang_barcode_reading_rules_list";
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<List<BarcodeReadingSetupModel>> GetRulesAsync()
    {
        var stored = await cache.GetAsync<List<BarcodeReadingSetupModel>>(RulesStorageKey);
        if (stored is not null)
        {
            return stored.Select(Normalize)
                .OrderByDescending(x => x.Prefix.Length)
                .ThenBy(x => x.Prefix)
                .ToList();
        }

        var seeds = GetDefaultRules();
        await cache.SetAsync(RulesStorageKey, seeds);
        return seeds;
    }

    public async Task<ParsedBarcodeResult> ParseBarcodeAsync(string scannedCode)
    {
        if (string.IsNullOrWhiteSpace(scannedCode))
        {
            return Failure(string.Empty, "Empty barcode.");
        }

        var code = scannedCode.Trim();
        var rules = (await GetRulesAsync())
            .Where(x => x.IsActive && code.StartsWith(x.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Prefix.Length)
            .ToList();

        if (rules.Count == 0)
        {
            return Failure(code, "No active barcode rule matches this prefix.");
        }

        foreach (var rule in rules)
        {
            var itemStart = rule.BarcodeIncludesPrefix ? 0 : rule.Prefix.Length;
            var quantityLength = rule.IsQuantityFixedAsOne ? 0 : rule.QuantityLength;
            var minimumLength = itemStart + rule.ItemCodeLength + quantityLength +
                                rule.PriceLength + rule.ChecksumLength;

            if (code.Length < minimumLength) continue;

            var cursor = itemStart;
            var itemCode = code.Substring(cursor, rule.ItemCodeLength).Trim();
            cursor += rule.ItemCodeLength;
            if (string.IsNullOrWhiteSpace(itemCode)) continue;

            var quantity = 1m;
            if (quantityLength > 0)
            {
                var quantityText = code.Substring(cursor, quantityLength);
                if (!TryParseSegment(quantityText, rule.DecimalPlace, out quantity)) continue;
                cursor += quantityLength;
            }

            decimal? price = null;
            if (rule.PriceLength > 0)
            {
                var priceText = code.Substring(cursor, rule.PriceLength);
                if (!TryParseSegment(priceText, rule.DecimalPlace, out var parsedPrice)) continue;
                price = parsedPrice;
            }

            return new ParsedBarcodeResult
            {
                Success = true,
                ItemCode = itemCode,
                Quantity = quantity > 0 ? quantity : 1,
                Price = price,
                MatchedRule = rule,
                RawCode = code,
                StatusMessage = $"Matched prefix {rule.Prefix}."
            };
        }

        return Failure(code, "The barcode is shorter than the configured rule or contains an invalid numeric segment.");
    }

    public async Task<bool> SaveRuleAsync(BarcodeReadingSetupModel rule, bool isNew)
    {
        if (!TryNormalizeAndValidate(rule, out var normalized)) return false;

        await _writeLock.WaitAsync();
        try
        {
            var rules = await GetRulesAsync();
            var index = rules.FindIndex(x =>
                x.Prefix.Equals(normalized.Prefix, StringComparison.OrdinalIgnoreCase));

            if (isNew && index >= 0) return false;

            if (index >= 0) rules[index] = normalized;
            else rules.Add(normalized);

            await cache.SetAsync(
                RulesStorageKey,
                rules.OrderByDescending(x => x.Prefix.Length).ThenBy(x => x.Prefix).ToList());
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteRuleAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return false;

        await _writeLock.WaitAsync();
        try
        {
            var rules = await GetRulesAsync();
            var removed = rules.RemoveAll(x =>
                x.Prefix.Equals(prefix.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;

            await cache.SetAsync(RulesStorageKey, rules);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<BarcodeReadingSetupModel> GetSetupAsync() =>
        (await GetRulesAsync()).FirstOrDefault() ?? new BarcodeReadingSetupModel();

    public Task<bool> SaveSetupAsync(BarcodeReadingSetupModel setup) =>
        SaveRuleAsync(setup, false);

    private static bool TryParseSegment(string text, int decimalPlaces, out decimal result)
    {
        result = 0;
        if (text.Any(x => !char.IsDigit(x)) ||
            !decimal.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var raw))
        {
            return false;
        }

        result = raw / Pow10(decimalPlaces);
        return true;
    }

    private static decimal Pow10(int places)
    {
        var result = 1m;
        for (var i = 0; i < places; i++) result *= 10m;
        return result;
    }

    private static bool TryNormalizeAndValidate(
        BarcodeReadingSetupModel? source,
        out BarcodeReadingSetupModel normalized)
    {
        normalized = source is null ? new BarcodeReadingSetupModel() : Normalize(source);
        if (string.IsNullOrWhiteSpace(normalized.Prefix) || normalized.Prefix.Length != 2) return false;
        if (normalized.Prefix.Any(char.IsWhiteSpace)) return false;
        if (normalized.ItemCodeLength is < 1 or > 64) return false;
        if (normalized.QuantityLength is < 0 or > 18) return false;
        if (normalized.PriceLength is < 0 or > 18) return false;
        if (normalized.DecimalPlace is < 0 or > 6) return false;
        if (normalized.ChecksumLength is < 0 or > 4) return false;
        if (!normalized.IsQuantityFixedAsOne && normalized.QuantityLength == 0) return false;
        return true;
    }

    private static BarcodeReadingSetupModel Normalize(BarcodeReadingSetupModel source) => new()
    {
        Prefix = (source.Prefix ?? string.Empty).Trim().ToUpperInvariant(),
        IsActive = source.IsActive,
        BarcodeType = string.IsNullOrWhiteSpace(source.BarcodeType) ? "BarCode" : source.BarcodeType.Trim(),
        SelectionType = source.SelectionType == "QRCode" ? "QRCode" : "Barcode",
        ItemCodeLength = source.ItemCodeLength,
        QuantityLength = source.IsQuantityFixedAsOne ? 0 : source.QuantityLength,
        IsQuantityFixedAsOne = source.IsQuantityFixedAsOne,
        PriceLength = source.PriceLength,
        DecimalPlace = source.DecimalPlace,
        ChecksumLength = source.ChecksumLength,
        BarcodeIncludesPrefix = source.BarcodeIncludesPrefix,
        HasOverlappingBarcodeItem = source.HasOverlappingBarcodeItem
    };

    private static ParsedBarcodeResult Failure(string code, string message) => new()
    {
        Success = false,
        RawCode = code,
        ItemCode = code,
        Quantity = 1,
        StatusMessage = message
    };

    private static List<BarcodeReadingSetupModel> GetDefaultRules() =>
    [
        new()
        {
            Prefix = "21",
            BarcodeType = "BarCode",
            SelectionType = "Barcode",
            ItemCodeLength = 5,
            QuantityLength = 5,
            IsQuantityFixedAsOne = false,
            PriceLength = 0,
            DecimalPlace = 2,
            ChecksumLength = 1,
            BarcodeIncludesPrefix = false,
            IsActive = true
        },
        new()
        {
            Prefix = "22",
            BarcodeType = "BarCode",
            SelectionType = "Barcode",
            ItemCodeLength = 5,
            QuantityLength = 0,
            IsQuantityFixedAsOne = true,
            PriceLength = 5,
            DecimalPlace = 2,
            ChecksumLength = 1,
            BarcodeIncludesPrefix = true,
            IsActive = true
        }
    ];
}
