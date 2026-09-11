using System;
using System.Collections.Generic;
using System.Linq;

namespace SenangRetails.Shared.Models.DTOs
{
    public class DiscountSetupModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public int SupportingTableTypeId { get; set; }

        // Section 1: General Setup
        public string Description { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public bool IsAtCost { get; set; } = false;
        public bool IsOpenDiscount { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public string DiscountMethod { get; set; } = "Percent %"; // "Percent %", "Amount", "Nos", "Compound"
        public decimal Amount { get; set; } = 0.00m;
        public decimal NumberField { get; set; }
        public string DiscountFormula { get; set; } = string.Empty;

        // Availability fields with documented API names.
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<string> WeekDays { get; set; } = ["Mon", "Tues", "Wed", "Thurs", "Fri", "Satur", "Sun"];

        // Preserve these API values without assigning undocumented client-side meanings.
        public string FrequencyType { get; set; } = string.Empty;
        public int FrequencyTypeId { get; set; }
        public int Frequency { get; set; }

        public bool CanApplyInBilling =>
            IsOpenDiscount || IsAtCost ||
            DiscountMethod is "Percent %" or "Amount" or "Compound";

        // Helper presentation properties
        public string FormattedAmount
        {
            get
            {
                if (IsOpenDiscount)
                {
                    return "Open Rate";
                }

                if (IsAtCost)
                {
                    return "At Cost";
                }

                return DiscountMethod switch
                {
                    "Percent %" => $"{Amount:0.##}%",
                    "Amount" => $"RM {Amount:N2}",
                    "Nos" => $"{Amount:0.##} Nos",
                    "Compound" => string.IsNullOrWhiteSpace(DiscountFormula) ? "Compound" : DiscountFormula,
                    _ => Amount.ToString("N2")
                };
            }
        }

        public string FormattedWeekDays
        {
            get
            {
                if (WeekDays == null || WeekDays.Count == 0) return "No days selected";
                if (WeekDays.Count == 7) return "All Days";
                return string.Join(", ", WeekDays);
            }
        }

        public string FormattedDateRange
        {
            get
            {
                if (!DateFrom.HasValue && !DateTo.HasValue) return "Always Available";
                if (DateFrom.HasValue && !DateTo.HasValue) return $"From {DateFrom.Value:dd/MM/yyyy}";
                if (!DateFrom.HasValue && DateTo.HasValue) return $"Until {DateTo.Value:dd/MM/yyyy}";
                return $"{DateFrom!.Value:dd/MM/yyyy} - {DateTo!.Value:dd/MM/yyyy}";
            }
        }

        public DiscountSetupModel Clone()
        {
            return new DiscountSetupModel
            {
                Id = this.Id,
                SupportingTableTypeId = this.SupportingTableTypeId,
                Description = this.Description,
                BranchId = this.BranchId,
                IsAtCost = this.IsAtCost,
                IsOpenDiscount = this.IsOpenDiscount,
                IsActive = this.IsActive,
                DiscountMethod = this.DiscountMethod,
                Amount = this.Amount,
                NumberField = this.NumberField,
                DiscountFormula = this.DiscountFormula,
                DateFrom = this.DateFrom,
                DateTo = this.DateTo,
                WeekDays = new List<string>(this.WeekDays ?? new List<string>()),
                FrequencyType = this.FrequencyType,
                FrequencyTypeId = this.FrequencyTypeId,
                Frequency = this.Frequency
            };
        }
    }
}
