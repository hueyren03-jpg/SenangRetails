using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class PromotionSetupModel
    {
        public string MasterAccountID { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PromoType { get; set; } = "Percentage"; // "Percentage" or "Fixed Price"
        public string PromoMethod { get; set; } = "Discount %";
        public string Rounding { get; set; } = "No Rounding";
        public string PromoCondition { get; set; } = "Total Price After Discount, Disc To All Items";
        public decimal DiscountValue { get; set; } = 0.00m;
        public DateTime? StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; } = DateTime.Today.AddMonths(1);
        public int MinQuantity { get; set; } = 1;
        public int MaxLimitPerOrder { get; set; } = 0; // 0 = Unlimited
        public bool IsActive { get; set; } = true;
        public string Remarks { get; set; } = string.Empty;
        public string BranchID { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> AvailableBranchIds { get; set; } = new();
        public TimeSpan AvailableTimeFrom { get; set; } = TimeSpan.Zero;
        public TimeSpan AvailableTimeTo { get; set; } = new(23, 59, 59);
        public decimal PromoPriority { get; set; }
        public decimal MaxDiscountLimit { get; set; }
        public decimal PromoConditionAmount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalActualValue { get; set; }
        public int PackageQuantityTypeID { get; set; } = 1;
        public bool IsConfirmed { get; set; } = true;
        public bool IsDeferred { get; set; }
        public string OptionItems { get; set; } = string.Empty;
        public string OptionGroups { get; set; } = string.Empty;
        public string OptionBrands { get; set; } = string.Empty;
        public string HeaderCaption { get; set; } = string.Empty;
        public string CustomRules { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> AppliedProductIds { get; set; } = new();
        public bool IsBuyGetPromotion { get; set; }
        public int RewardQuantity { get; set; }
    }
}
