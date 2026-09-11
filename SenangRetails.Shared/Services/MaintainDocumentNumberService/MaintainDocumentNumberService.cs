using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Shared.Services.MaintainDocumentNumberService
{
    public class MaintainDocumentNumberService : IMaintainDocumentNumberService
    {
        private readonly ILocalJsonCache _cache;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private const string StorageKey = "senang_maintain_doc_numbers";

        public MaintainDocumentNumberService(ILocalJsonCache cache)
        {
            _cache = cache;
        }

        public async Task<List<MaintainDocumentNumberModel>> GetDocumentNumbersAsync(string? category = null)
        {
            var list = await _cache.GetAsync<List<MaintainDocumentNumberModel>>(StorageKey);
            if (list is null)
            {
                list = GetSeedData();
                await _cache.SetAsync(StorageKey, list);
            }

            var normalized = list.Select(Normalize).ToList();
            return string.IsNullOrWhiteSpace(category)
                ? normalized
                : normalized.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<bool> SaveDocumentNumbersAsync(List<MaintainDocumentNumberModel> list)
        {
            if (list is null || list.Count == 0) return false;
            var normalizedInput = list.Select(Normalize).ToList();
            if (normalizedInput.Any(x => !IsValid(x))) return false;

            await _writeLock.WaitAsync();
            try
            {
                var fullList = await _cache.GetAsync<List<MaintainDocumentNumberModel>>(StorageKey)
                    ?? GetSeedData();

                foreach (var item in normalizedInput)
                {
                    var existing = fullList.FirstOrDefault(x =>
                        x.DocType == item.DocType &&
                        x.Category.Equals(item.Category, StringComparison.OrdinalIgnoreCase) &&
                        x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Prefix = item.Prefix;
                        existing.LastNumberUsed = item.LastNumberUsed;
                        existing.CharacterCount = item.CharacterCount;
                    }
                    else
                    {
                        fullList.Add(item);
                    }
                }

                await _cache.SetAsync(StorageKey, fullList.Select(Normalize).ToList());
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<string?> ReserveNextNumberAsync(string category, int docType, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;

            await _writeLock.WaitAsync();
            try
            {
                var fullList = await _cache.GetAsync<List<MaintainDocumentNumberModel>>(StorageKey)
                    ?? GetSeedData();
                var rule = fullList.FirstOrDefault(x =>
                    x.DocType == docType &&
                    x.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(name) || x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
                if (rule is null || !IsValid(Normalize(rule))) return null;

                checked
                {
                    rule.LastNumberUsed++;
                }

                await _cache.SetAsync(StorageKey, fullList.Select(Normalize).ToList());
                return Normalize(rule).GetFormattedPreview();
            }
            catch (OverflowException)
            {
                return null;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static MaintainDocumentNumberModel Normalize(MaintainDocumentNumberModel item) => new()
        {
            DocType = item.DocType,
            Category = string.IsNullOrWhiteSpace(item.Category) ? "Document" : item.Category.Trim(),
            Name = (item.Name ?? string.Empty).Trim(),
            Prefix = (item.Prefix ?? string.Empty).Trim().ToUpperInvariant(),
            LastNumberUsed = Math.Max(0, item.LastNumberUsed),
            CharacterCount = Math.Clamp(item.CharacterCount, 2, 30)
        };

        private static bool IsValid(MaintainDocumentNumberModel item) =>
            !string.IsNullOrWhiteSpace(item.Category) &&
            !string.IsNullOrWhiteSpace(item.Name) &&
            item.Prefix.Length <= 12 &&
            item.Prefix.All(x => !char.IsWhiteSpace(x)) &&
            item.LastNumberUsed >= 0 &&
            item.CharacterCount is >= 2 and <= 30 &&
            item.CharacterCount > item.Prefix.Length;

        private List<MaintainDocumentNumberModel> GetSeedData()
        {
            return new List<MaintainDocumentNumberModel>
            {
                // --- DOCUMENT TYPES ---
                new MaintainDocumentNumberModel { DocType = 0, Category = "Document", Name = "InvalidDocument", Prefix = "", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 1, Category = "Document", Name = "SalesQuotation", Prefix = "SQ", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 2, Category = "Document", Name = "InventoryAdjustment", Prefix = "IA", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 3, Category = "Document", Name = "SalesOrder", Prefix = "SO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 4, Category = "Document", Name = "Invoice", Prefix = "INV", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 6, Category = "Document", Name = "Customer Debit Note", Prefix = "DN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 7, Category = "Document", Name = "Customer Credit Note", Prefix = "CN", LastNumberUsed = 4, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 8, Category = "Document", Name = "Purchase Order", Prefix = "HQPO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 10, Category = "Document", Name = "Journal Entry", Prefix = "JN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 13, Category = "Document", Name = "Supplier Debit Note", Prefix = "DN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 14, Category = "Document", Name = "Supplier Credit Note", Prefix = "CN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 15, Category = "Document", Name = "Stock Transfer", Prefix = "GT", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 16, Category = "Document", Name = "Cash Purchase", Prefix = "HQCP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 17, Category = "Document", Name = "Fund Transfer", Prefix = "FTR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 19, Category = "Document", Name = "Deposit", Prefix = "DEP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 20, Category = "Document", Name = "Credit Sales Finance Charge", Prefix = "INT", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 23, Category = "Document", Name = "Customer Bad Debt", Prefix = "BD", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 24, Category = "Document", Name = "Customer Credit Allocation", Prefix = "AL", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 25, Category = "Document", Name = "Cash Book Payment", Prefix = "CBP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 26, Category = "Document", Name = "Cash Book Receipt", Prefix = "CBR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 30, Category = "Document", Name = "Bank Reconciliation", Prefix = "BR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 31, Category = "Document", Name = "Inventory Adjustment", Prefix = "HQIA", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 33, Category = "Document", Name = "Point Redemption", Prefix = "PR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 34, Category = "Document", Name = "Customer Point", Prefix = "CP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 37, Category = "Document", Name = "Delivery Order", Prefix = "DO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 39, Category = "Document", Name = "Stock Return Credit Note", Prefix = "CN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 42, Category = "Document", Name = "Academy Invoice", Prefix = "AI", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 45, Category = "Document", Name = "Instalment", Prefix = "IST", LastNumberUsed = 0, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 47, Category = "Document", Name = "Instalment Receipt", Prefix = "IR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 49, Category = "Document", Name = "Academy Enrolment", Prefix = "E", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 49, Category = "Document", Name = "GST Adjustment", Prefix = "GAJ", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 50, Category = "Document", Name = "Service Job Sheet", Prefix = "JS", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 51, Category = "Document", Name = "GRN", Prefix = "GRN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 53, Category = "Document", Name = "POSReceipt", Prefix = "POSR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 57, Category = "Document", Name = "GIN", Prefix = "GI", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 58, Category = "Document", Name = "Purchase Request", Prefix = "PR", LastNumberUsed = 1, CharacterCount = 15 },

                // --- ACCOUNT TYPES ---
                new MaintainDocumentNumberModel { DocType = 1, Category = "Account", Name = "Financial Account", Prefix = "F", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 2, Category = "Account", Name = "Bank Account", Prefix = "BNK", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 3, Category = "Account", Name = "Customer", Prefix = "CA", LastNumberUsed = 8, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 4, Category = "Account", Name = "Inventory", Prefix = "STK", LastNumberUsed = 17, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 5, Category = "Account", Name = "Job Account", Prefix = "JB", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 6, Category = "Account", Name = "Employee", Prefix = "HR", LastNumberUsed = 6, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 7, Category = "Account", Name = "Sales Tax Authority", Prefix = "TAX", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 8, Category = "Account", Name = "Creditor", Prefix = "CR", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 9, Category = "Account", Name = "Student", Prefix = "S", LastNumberUsed = 8, CharacterCount = 10 }
            };
        }
    }
}
