using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.DiscountSetupService
{
    public sealed class DiscountSetupService(ILocalJsonCache cache) : IDiscountSetupService
    {
        private const string StorageKey = "senang_discount_setup_rules";
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public string? LastError { get; private set; }

        private static readonly List<DiscountSetupModel> SeedDiscounts =
        [
            new()
            {
                Id = "dsc_seed_50",
                Description = "Discount 50%",
                IsAtCost = false,
                IsOpenDiscount = false,
                IsActive = true,
                DiscountMethod = "Percent %",
                Amount = 50.00m,
                DateFrom = null,
                DateTo = null,
                WeekDays = ["Mon", "Tues", "Wed", "Thurs", "Fri", "Satur", "Sun"],
                FrequencyType = "None"
            },
            new()
            {
                Id = "dsc_seed_ni",
                Description = "Ni",
                IsAtCost = false,
                IsOpenDiscount = false,
                IsActive = true,
                DiscountMethod = "Percent %",
                Amount = 10.00m,
                DateFrom = null,
                DateTo = null,
                WeekDays = ["Mon", "Tues", "Wed", "Thurs", "Fri", "Satur", "Sun"],
                FrequencyType = "None"
            }
        ];

        public async Task<List<DiscountSetupModel>> GetDiscountsAsync()
        {
            try
            {
                var stored = await cache.GetAsync<List<DiscountSetupModel>>(StorageKey);
                if (stored is not null)
                {
                    bool needsUpdate = false;

                    // Remove old placeholder seeds if present
                    int removedOld = stored.RemoveAll(x => x.Id is "dsc_seed_01" or "dsc_seed_02" or "dsc_seed_03" or "dsc_seed_04" or "dsc_seed_05");
                    if (removedOld > 0)
                    {
                        needsUpdate = true;
                    }

                    // Update existing Ni record if it was seeded with 0 amount
                    var existingNi = stored.FirstOrDefault(x => x.Description.Equals("Ni", StringComparison.OrdinalIgnoreCase));
                    if (existingNi != null && existingNi.Amount == 0m && existingNi.IsOpenDiscount)
                    {
                        existingNi.Amount = 10.00m;
                        existingNi.IsOpenDiscount = false;
                        needsUpdate = true;
                    }

                    foreach (var seed in SeedDiscounts)
                    {
                        if (!stored.Any(x => x.Description.Equals(seed.Description, StringComparison.OrdinalIgnoreCase)))
                        {
                            stored.Add(Normalize(seed));
                            needsUpdate = true;
                        }
                    }
                    if (needsUpdate)
                    {
                        await cache.SetAsync(StorageKey, stored);
                    }
                    LastError = null;
                    return stored.Select(Normalize).OrderBy(x => x.Description).ToList();
                }

                var seeds = SeedDiscounts.Select(Normalize).ToList();
                await cache.SetAsync(StorageKey, seeds);
                LastError = null;
                return seeds;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return [];
            }
        }

        public async Task<DiscountSetupModel?> GetDiscountAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var discounts = await GetDiscountsAsync();
            return discounts.FirstOrDefault(x => x.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> SaveDiscountAsync(DiscountSetupModel discount, bool isNew)
        {
            LastError = null;
            if (!TryNormalizeAndValidate(discount, out var normalized, out var validationError))
            {
                LastError = validationError;
                return false;
            }

            await _writeLock.WaitAsync();
            try
            {
                var discounts = await GetDiscountsAsync();
                var index = discounts.FindIndex(x => x.Id.Equals(normalized.Id, StringComparison.OrdinalIgnoreCase));

                if (isNew && index >= 0)
                {
                    // Generate new ID if duplicate
                    normalized.Id = Guid.NewGuid().ToString("N");
                    discounts.Add(normalized);
                }
                else if (isNew)
                {
                    discounts.Add(normalized);
                }
                else
                {
                    if (index < 0)
                    {
                        LastError = $"Discount record '{normalized.Description}' was not found.";
                        return false;
                    }
                    discounts[index] = normalized;
                }

                await cache.SetAsync(StorageKey, discounts);
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
            if (string.IsNullOrWhiteSpace(id)) return false;

            await _writeLock.WaitAsync();
            try
            {
                var discounts = await GetDiscountsAsync();
                var index = discounts.FindIndex(x => x.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
                if (index < 0) return false;

                discounts.RemoveAt(index);
                await cache.SetAsync(StorageKey, discounts);
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
            if (string.IsNullOrWhiteSpace(id)) return false;

            await _writeLock.WaitAsync();
            try
            {
                var discounts = await GetDiscountsAsync();
                var record = discounts.FirstOrDefault(x => x.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
                if (record == null) return false;

                record.IsActive = !record.IsActive;
                await cache.SetAsync(StorageKey, discounts);
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

        private static bool TryNormalizeAndValidate(
            DiscountSetupModel input,
            out DiscountSetupModel normalized,
            out string? error)
        {
            normalized = Normalize(input);

            if (string.IsNullOrWhiteSpace(normalized.Description))
            {
                error = "Description is required.";
                return false;
            }

            if (!normalized.IsOpenDiscount && normalized.Amount < 0)
            {
                error = "Amount cannot be negative.";
                return false;
            }

            if (normalized.DiscountMethod == "Percent %" && !normalized.IsOpenDiscount && normalized.Amount > 100)
            {
                error = "Percentage discount cannot exceed 100%.";
                return false;
            }

            if (normalized.DateFrom.HasValue && normalized.DateTo.HasValue &&
                normalized.DateTo.Value.Date < normalized.DateFrom.Value.Date)
            {
                error = "Available Date To must be on or after Available Date From.";
                return false;
            }

            error = null;
            return true;
        }

        private static DiscountSetupModel Normalize(DiscountSetupModel model)
        {
            var validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Percent %", "Amount", "Nos", "Compound"
            };

            var method = validMethods.FirstOrDefault(m => m.Equals(model.DiscountMethod?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Percent %";

            var weekDays = (model.WeekDays ?? new List<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (weekDays.Count == 0)
            {
                weekDays = ["Mon", "Tues", "Wed", "Thurs", "Fri", "Satur", "Sun"];
            }

            return new DiscountSetupModel
            {
                Id = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString("N") : model.Id.Trim(),
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
    }
}
