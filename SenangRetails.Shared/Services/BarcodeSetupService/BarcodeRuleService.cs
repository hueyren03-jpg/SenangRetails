using System.Globalization;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services.BarcodeSetupService;

public sealed class BarcodeSetupService(BarcodeFormatAC apiClient) : IBarcodeSetupService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, BarcodeFormatApiModel> _apiRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private List<BarcodeReadingSetupModel> _rulesCache = [];

    public string? LastError { get; private set; }

    public async Task<List<BarcodeReadingSetupModel>> GetRulesAsync()
    {
        LastError = null;

        try
        {
            var response = await apiClient.LoadProxyAsync();
            if (!IsSuccessful(response))
            {
                LastError = apiClient.LastError
                    ?? response?.message
                    ?? "Failed to load barcode format records.";

                return _rulesCache.Select(Normalize)
                    .OrderByDescending(x => x.Prefix.Length)
                    .ThenBy(x => x.Prefix)
                    .ToList();
            }

            var records = response?.result ?? [];
            _apiRecords.Clear();

            foreach (var record in records)
            {
                var prefix = (record.Prefix ?? string.Empty).Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(prefix))
                    _apiRecords[prefix] = CloneApi(record);
            }

            _rulesCache = records
                .Select(FromApi)
                .OrderByDescending(x => x.Prefix.Length)
                .ThenBy(x => x.Prefix)
                .ToList();

            return _rulesCache.Select(Normalize).ToList();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return _rulesCache.Select(Normalize)
                .OrderByDescending(x => x.Prefix.Length)
                .ThenBy(x => x.Prefix)
                .ToList();
        }
    }

    public async Task<ParsedBarcodeResult> ParseBarcodeAsync(string scannedCode)
    {
        if (string.IsNullOrWhiteSpace(scannedCode))
            return Failure(string.Empty, "Empty barcode.");

        var code = scannedCode.Trim();
        var sourceRules = _rulesCache.Count > 0
            ? _rulesCache
            : await GetRulesAsync();

        var rules = sourceRules
            .Where(x => x.IsActive && code.StartsWith(x.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Prefix.Length)
            .ToList();

        if (rules.Count == 0)
            return Failure(code, "No active barcode rule matches this prefix.");

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

        return Failure(
            code,
            "The barcode is shorter than the configured rule or contains an invalid numeric segment.");
    }

    public async Task<bool> SaveRuleAsync(BarcodeReadingSetupModel rule, bool isNew)
    {
        LastError = null;

        if (!TryNormalizeAndValidate(rule, out var normalized, out var validationError))
        {
            LastError = validationError;
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            if (_apiRecords.Count == 0)
                await GetRulesAsync();

            if (isNew && _apiRecords.ContainsKey(normalized.Prefix))
            {
                LastError = $"Prefix {normalized.Prefix} already exists.";
                return false;
            }

            BarcodeFormatApiModel? existing = null;
            if (!isNew)
            {
                _apiRecords.TryGetValue(normalized.Prefix, out existing);

                if (existing is null)
                {
                    var loadResponse = await apiClient.LoadRecordAsync(normalized.Prefix);
                    if (IsSuccessful(loadResponse))
                        existing = loadResponse?.result;
                }
            }

            var request = ToApi(normalized, existing, isNew);
            IApiResponse? response = isNew
                ? await apiClient.CreateRecordAsync(request)
                : await apiClient.UpdateRecordAsync(request);

            if (!IsSuccessful(response))
            {
                LastError = apiClient.LastError
                    ?? response?.message
                    ?? "The BarcodeFormat API rejected the save request.";
                return false;
            }

            _apiRecords[normalized.Prefix] = CloneApi(request);

            var index = _rulesCache.FindIndex(x =>
                x.Prefix.Equals(normalized.Prefix, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
                _rulesCache[index] = Normalize(normalized);
            else
                _rulesCache.Add(Normalize(normalized));

            _rulesCache = _rulesCache
                .OrderByDescending(x => x.Prefix.Length)
                .ThenBy(x => x.Prefix)
                .ToList();

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

    public async Task<bool> DeleteRuleAsync(string prefix)
    {
        LastError = null;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            LastError = "Prefix is required.";
            return false;
        }

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();

        await _writeLock.WaitAsync();
        try
        {
            var response = await apiClient.DeleteRecordAsync(normalizedPrefix);
            if (!IsSuccessful(response))
            {
                LastError = apiClient.LastError
                    ?? response?.message
                    ?? "The BarcodeFormat API rejected the delete request.";
                return false;
            }

            _apiRecords.Remove(normalizedPrefix);
            _rulesCache.RemoveAll(x =>
                x.Prefix.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase));
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

    public async Task<BarcodeReadingSetupModel> GetSetupAsync() =>
        (await GetRulesAsync()).FirstOrDefault() ?? new BarcodeReadingSetupModel();

    public Task<bool> SaveSetupAsync(BarcodeReadingSetupModel setup) =>
        SaveRuleAsync(setup, false);

    private static BarcodeReadingSetupModel FromApi(BarcodeFormatApiModel source) =>
        Normalize(new BarcodeReadingSetupModel
        {
            Prefix = source.Prefix ?? string.Empty,
            IsActive = source.IsActive,
            BarcodeType = string.IsNullOrWhiteSpace(source.BarcodeType) ? "BarCode" : source.BarcodeType,
            // BarcodeFormat API currently has no Barcode/QRCode selection field.
            SelectionType = "Barcode",
            ItemCodeLength = source.ItemCodeLength,
            QuantityLength = source.QuantityLength,
            IsQuantityFixedAsOne =
                string.Equals(source.QuantityFixedAsOne, "YES", StringComparison.OrdinalIgnoreCase),
            PriceLength = source.PriceLength,
            DecimalPlace = source.PriceDecimalPlace,
            ChecksumLength = source.CheckSumLength,
            BarcodeIncludesPrefix = source.IsBarcodeIncludesPrefix,
            HasOverlappingBarcodeItem = source.HasOverlappingBarcodeItems
        });

    private static BarcodeFormatApiModel ToApi(
        BarcodeReadingSetupModel source,
        BarcodeFormatApiModel? existing,
        bool isNew)
    {
        var request = existing is null
            ? new BarcodeFormatApiModel()
            : CloneApi(existing);

        request.IsLoading = false;
        request.Prefix = source.Prefix;
        request.IsActive = source.IsActive;
        request.ItemCodeLength = source.ItemCodeLength;
        request.QuantityLength = source.IsQuantityFixedAsOne ? 0 : source.QuantityLength;
        request.QuantityFixedAsOne = source.IsQuantityFixedAsOne ? "YES" : "NO";
        request.PriceLength = source.PriceLength;
        request.PriceDecimalPlace = source.DecimalPlace;
        request.CheckSumLength = source.ChecksumLength;
        request.BarcodeType = string.IsNullOrWhiteSpace(source.BarcodeType)
            ? "BarCode"
            : source.BarcodeType;
        request.HasOverlappingBarcodeItems = source.HasOverlappingBarcodeItem;
        request.IsBarcodeIncludesPrefix = source.BarcodeIncludesPrefix;
        request.SaveAction = isNew
            ? EBI.Enum.EntityState.Added
            : EBI.Enum.EntityState.Changed;
        request.IsDirty = true;

        return request;
    }

    private static BarcodeFormatApiModel CloneApi(BarcodeFormatApiModel source) => new()
    {
        IsLoading = source.IsLoading,
        Prefix = source.Prefix ?? string.Empty,
        IsActive = source.IsActive,
        ItemCodeLength = source.ItemCodeLength,
        QuantityLength = source.QuantityLength,
        QuantityFixedAsOne = source.QuantityFixedAsOne ?? "NO",
        PriceLength = source.PriceLength,
        PriceDecimalPlace = source.PriceDecimalPlace,
        CheckSumLength = source.CheckSumLength,
        SectionInventoryID = source.SectionInventoryID,
        SectionBarcode = source.SectionBarcode,
        SectionDescription = source.SectionDescription,
        SectionBatch = source.SectionBatch,
        SectionExpiryDate = source.SectionExpiryDate,
        SectionSerialNo = source.SectionSerialNo,
        SectionUOM = source.SectionUOM,
        SectionMatrix = source.SectionMatrix,
        BarcodeType = source.BarcodeType ?? "BarCode",
        HasOverlappingBarcodeItems = source.HasOverlappingBarcodeItems,
        IsBarcodeIncludesPrefix = source.IsBarcodeIncludesPrefix,
        SaveAction = source.SaveAction,
        IsDirty = source.IsDirty
    };

    private static bool TryNormalizeAndValidate(
        BarcodeReadingSetupModel? source,
        out BarcodeReadingSetupModel normalized,
        out string? error)
    {
        normalized = source is null ? new BarcodeReadingSetupModel() : Normalize(source);
        error = null;

        if (source is null)
            error = "Barcode rule is required.";
        else if (string.IsNullOrWhiteSpace(normalized.Prefix) || normalized.Prefix.Length != 2)
            error = "Prefix must contain exactly 2 characters.";
        else if (normalized.Prefix.Any(char.IsWhiteSpace))
            error = "Prefix cannot contain whitespace.";
        else if (normalized.ItemCodeLength is < 1 or > 64)
            error = "Item code length must be between 1 and 64.";
        else if (normalized.QuantityLength is < 0 or > 18)
            error = "Quantity length must be between 0 and 18.";
        else if (normalized.PriceLength is < 0 or > 18)
            error = "Price length must be between 0 and 18.";
        else if (normalized.DecimalPlace is < 0 or > 6)
            error = "Decimal place must be between 0 and 6.";
        else if (normalized.ChecksumLength is < 0 or > 5)
            error = "Checksum length must be between 0 and 5.";

        return error is null;
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

    private static bool IsSuccessful(IApiResponse? response) =>
        response?.statusCode is >= 200 and < 300;

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

    private static ParsedBarcodeResult Failure(string code, string message) => new()
    {
        Success = false,
        RawCode = code,
        ItemCode = code,
        Quantity = 1,
        StatusMessage = message
    };
}
