using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.CashDiscountService;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.DiscountSetupService;

public sealed class ApiDiscountSetupService(
    ICashDiscountService cashDiscountApi,
    ILocalJsonCache cache) : IDiscountSetupService
{
    private const string StorageKey = "senang_cash_discount_api_cache_v1";
    private static readonly DateTime DefaultStartDate = new(2020, 1, 1);
    private static readonly DateTime DefaultEndDate = new(2049, 12, 31);
    private static readonly string[] OrderedWeekDays =
        ["Mon", "Tues", "Wed", "Thurs", "Fri", "Satur", "Sun"];
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string? LastError { get; private set; }

    public async Task<List<DiscountSetupModel>> GetDiscountsAsync()
    {
        LastError = null;

        try
        {
            var remote = await cashDiscountApi.LoadProxy();
            if (string.IsNullOrWhiteSpace(cashDiscountApi.LastError))
            {
                var mapped = remote
                    .Select(FromApi)
                    .OrderBy(x => x.Description)
                    .ToList();

                await TryCacheAsync(mapped);
                return mapped;
            }

            var cached = await cache.GetAsync<List<DiscountSetupModel>>(StorageKey);
            LastError = cached is { Count: > 0 }
                ? $"{cashDiscountApi.LastError} Showing the last synchronized discount rules."
                : cashDiscountApi.LastError;

            return cached?.Select(Normalize).OrderBy(x => x.Description).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            var cached = await cache.GetAsync<List<DiscountSetupModel>>(StorageKey);
            return cached?.Select(Normalize).OrderBy(x => x.Description).ToList() ?? [];
        }
    }

    public async Task<DiscountSetupModel?> GetDiscountAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var discounts = await GetDiscountsAsync();
        return discounts.FirstOrDefault(x =>
            x.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SaveDiscountAsync(DiscountSetupModel discount, bool isNew)
    {
        LastError = null;
        if (!TryNormalizeAndValidate(discount, out var normalized, out var validationError))
        {
            LastError = validationError;
            return false;
        }

        if (!isNew && string.IsNullOrWhiteSpace(normalized.Id))
        {
            LastError = "The discount ID is required for an update.";
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            var request = ToApi(normalized, isNew);
            IApiResponse? response;

            if (isNew)
            {
                var createResponse = await cashDiscountApi.CreateRecord(request);
                response = createResponse;
                if (IsSuccessful(createResponse) &&
                    !string.IsNullOrWhiteSpace(createResponse?.result?.Id))
                {
                    normalized.Id = createResponse.result.Id;
                }
            }
            else
            {
                response = await cashDiscountApi.UpdateRecord(request);
            }

            if (!IsSuccessful(response))
            {
                LastError = cashDiscountApi.LastError
                    ?? response?.message
                    ?? "The CashDiscount API rejected the save request.";
                return false;
            }

            await UpsertCacheAsync(normalized);
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

    public async Task<bool> DeleteDiscountAsync(string id)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            LastError = "The discount ID is required.";
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            var response = await cashDiscountApi.Delete(id.Trim());
            if (!IsSuccessful(response))
            {
                LastError = cashDiscountApi.LastError
                    ?? response?.message
                    ?? "The CashDiscount API rejected the delete request.";
                return false;
            }

            await RemoveFromCacheAsync(id);
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

    public async Task<bool> ToggleActiveAsync(string id)
    {
        var discount = await GetDiscountAsync(id);
        if (discount is null)
        {
            LastError ??= "The discount record was not found.";
            return false;
        }

        discount.IsActive = !discount.IsActive;
        return await SaveDiscountAsync(discount, false);
    }

    private static bool TryNormalizeAndValidate(
        DiscountSetupModel input,
        out DiscountSetupModel normalized,
        out string? error)
    {
        normalized = Normalize(input);
        error = null;

        if (string.IsNullOrWhiteSpace(normalized.Description))
            error = "Description is required.";
        else if (!normalized.IsOpenDiscount && !normalized.IsAtCost &&
                 normalized.DiscountMethod != "Compound" && normalized.Amount < 0)
            error = "Amount cannot be negative.";
        else if (normalized.DiscountMethod == "Percent %" &&
                 !normalized.IsOpenDiscount && !normalized.IsAtCost && normalized.Amount > 100)
            error = "Percentage discount cannot exceed 100%.";
        else if (normalized.DiscountMethod == "Compound" &&
                 !normalized.IsOpenDiscount && !normalized.IsAtCost &&
                 string.IsNullOrWhiteSpace(normalized.DiscountFormula))
            error = "Compound discount formula is required.";
        else if (normalized.DateFrom.HasValue && normalized.DateTo.HasValue &&
                 normalized.DateTo.Value.Date < normalized.DateFrom.Value.Date)
            error = "Available Date To must be on or after Available Date From.";

        return error is null;
    }

    private static DiscountSetupModel Normalize(DiscountSetupModel model)
    {
        var method = model.DiscountMethod?.Trim() switch
        {
            "Amount" => "Amount",
            "Nos" => "Nos",
            "Compound" => "Compound",
            _ => "Percent %"
        };

        var weekDays = NormalizeWeekDays(model.WeekDays);

        return new DiscountSetupModel
        {
            Id = model.Id?.Trim() ?? string.Empty,
            SupportingTableTypeId = model.SupportingTableTypeId,
            Description = model.Description?.Trim() ?? string.Empty,
            BranchId = model.BranchId?.Trim() ?? string.Empty,
            IsAtCost = model.IsAtCost,
            IsOpenDiscount = model.IsOpenDiscount,
            IsActive = model.IsActive,
            DiscountMethod = method,
            Amount = Math.Max(0, model.Amount),
            NumberField = Math.Max(0, model.NumberField),
            DiscountFormula = model.DiscountFormula?.Trim() ?? string.Empty,
            DateFrom = model.DateFrom?.Date,
            DateTo = model.DateTo?.Date,
            WeekDays = weekDays,
            FrequencyType = model.FrequencyType?.Trim() ?? string.Empty,
            FrequencyTypeId = model.FrequencyTypeId,
            Frequency = model.Frequency
        };
    }

    private static DiscountSetupModel FromApi(CashDiscountDM source)
    {
        var method = FromDiscountTypeId(source.CashDiscountTypeID);
        var amount = method switch
        {
            "Percent %" => source.CashDiscountPercentage * 100m,
            "Amount" => source.CashDiscountPercentage,
            "Nos" => source.NumberField,
            _ => 0m
        };

        return Normalize(new DiscountSetupModel
        {
            Id = source.SupportingTableID ?? string.Empty,
            SupportingTableTypeId = source.SupportingTableTypeID,
            Description = source.SupportingTableName ?? string.Empty,
            BranchId = source.BranchID ?? string.Empty,
            IsAtCost = source.IsUseCost,
            IsOpenDiscount = source.IsOpenDiscount,
            IsActive = source.Active,
            DiscountMethod = method,
            Amount = amount,
            NumberField = source.NumberField,
            DiscountFormula = source.CashDiscountFormula ?? string.Empty,
            DateFrom = source.StartDate,
            DateTo = source.EndDate,
            WeekDays = FromApiWeekDays(source.WeekDays),
            FrequencyType = string.Empty,
            FrequencyTypeId = source.FrequencyType,
            Frequency = source.Frequency
        });
    }

    private static CashDiscountDM ToApi(DiscountSetupModel source, bool isNew)
    {
        var typeId = ToDiscountTypeId(source.DiscountMethod);
        var hasFixedValue = !source.IsOpenDiscount && !source.IsAtCost;

        return new CashDiscountDM
        {
            IsLoading = false,
            SupportingTableID = isNew ? string.Empty : source.Id,
            SupportingTableName = source.Description,
            SupportingTableTypeID = source.SupportingTableTypeId,
            Active = source.IsActive,
            BranchID = string.IsNullOrWhiteSpace(source.BranchId) ? null! : source.BranchId,
            NumberField = hasFixedValue && typeId == 3 ? source.Amount : source.NumberField,
            CashDiscountPercentage = hasFixedValue
                ? typeId switch
                {
                    1 => source.Amount / 100m,
                    2 => source.Amount,
                    _ => 0m
                }
                : 0m,
            CashDiscountTypeID = typeId,
            IsUseCost = source.IsAtCost,
            CashDiscountFormula = hasFixedValue && typeId == 4
                ? source.DiscountFormula
                : null!,
            FrequencyType = source.FrequencyTypeId,
            Frequency = source.Frequency,
            StartDate = source.DateFrom?.Date ?? DefaultStartDate,
            EndDate = source.DateTo?.Date ?? DefaultEndDate,
            WeekDays = ToApiWeekDays(source.WeekDays),
            IsOpenDiscount = source.IsOpenDiscount,
            SaveAction = isNew ? EBI.Enum.EntityState.Added : EBI.Enum.EntityState.Changed,
            IsDirty = true
        };
    }

    private static int ToDiscountTypeId(string method) => method switch
    {
        "Amount" => 2,
        "Nos" => 3,
        "Compound" => 4,
        _ => 1
    };

    private static string FromDiscountTypeId(int typeId) => typeId switch
    {
        2 => "Amount",
        3 => "Nos",
        4 => "Compound",
        _ => "Percent %"
    };

    private static List<string> NormalizeWeekDays(IEnumerable<string>? values)
    {
        var selected = (values ?? [])
            .Select(ToUiWeekDay)
            .Where(x => x is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected.Count == 0
            ? OrderedWeekDays.ToList()
            : OrderedWeekDays.Where(selected.Contains).ToList();
    }

    private static List<string> FromApiWeekDays(string? value) =>
        NormalizeWeekDays((value ?? string.Empty)
            .Split(['_', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string ToApiWeekDays(IEnumerable<string>? values) =>
        string.Join("_", NormalizeWeekDays(values).Select(ToApiWeekDay));

    private static string? ToUiWeekDay(string? value) => value?.Trim() switch
    {
        "Monday" or "Mon" => "Mon",
        "Tuesday" or "Tue" or "Tues" => "Tues",
        "Wednesday" or "Wed" => "Wed",
        "Thursday" or "Thu" or "Thurs" => "Thurs",
        "Friday" or "Fri" => "Fri",
        "Saturday" or "Sat" or "Satur" => "Satur",
        "Sunday" or "Sun" => "Sun",
        _ => null
    };

    private static string ToApiWeekDay(string value) => value switch
    {
        "Mon" => "Monday",
        "Tues" => "Tuesday",
        "Wed" => "Wednesday",
        "Thurs" => "Thursday",
        "Fri" => "Friday",
        "Satur" => "Saturday",
        _ => "Sunday"
    };

    private static bool IsSuccessful(IApiResponse? response) =>
        response?.statusCode is >= 200 and < 300;

    private async Task TryCacheAsync(List<DiscountSetupModel> values)
    {
        try
        {
            await cache.SetAsync(StorageKey, values);
        }
        catch
        {
            // API data remains authoritative even when the optional offline cache cannot be refreshed.
        }
    }

    private async Task UpsertCacheAsync(DiscountSetupModel value)
    {
        try
        {
            var values = await cache.GetAsync<List<DiscountSetupModel>>(StorageKey) ?? [];
            var index = values.FindIndex(x =>
                !string.IsNullOrWhiteSpace(value.Id) &&
                x.Id.Equals(value.Id, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
                values[index] = value;
            else if (!string.IsNullOrWhiteSpace(value.Id))
                values.Add(value);

            await cache.SetAsync(StorageKey, values);
        }
        catch
        {
            // A successful server write must not be reported as failed because caching failed.
        }
    }

    private async Task RemoveFromCacheAsync(string id)
    {
        try
        {
            var values = await cache.GetAsync<List<DiscountSetupModel>>(StorageKey) ?? [];
            values.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            await cache.SetAsync(StorageKey, values);
        }
        catch
        {
            // The next online load will reconcile the local read cache.
        }
    }
}
