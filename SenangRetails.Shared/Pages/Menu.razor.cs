using System;
using Microsoft.AspNetCore.Components;

namespace SenangRetails.Shared.Pages
{
    public partial class Menu : BasePage
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        [SupplyParameterFromQuery(Name = "view")]
        public string? QueryView { get; set; }

        private string SelectedView { get; set; } = "Product";
        private bool IsSidebarOpen { get; set; } = false;
        private bool IsLeftPanelHidden { get; set; } = true;

        private bool IsCatalogSectionOpen { get; set; } = true;
        private bool IsClassificationSectionOpen { get; set; } = true;

        private void ToggleCatalogSection() => IsCatalogSectionOpen = !IsCatalogSectionOpen;
        private void ToggleClassificationSection() => IsClassificationSectionOpen = !IsClassificationSectionOpen;

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            if (!string.IsNullOrWhiteSpace(QueryView))
            {
                if (QueryView.Equals("Division", StringComparison.OrdinalIgnoreCase) ||
                    QueryView.Equals("ItemDivision", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Division";
                }
                else if (QueryView.Equals("Department", StringComparison.OrdinalIgnoreCase) ||
                         QueryView.Equals("ItemDepartment", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Department";
                }
                else if (QueryView.Equals("Category", StringComparison.OrdinalIgnoreCase) ||
                         QueryView.Equals("ItemCategory", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Category";
                }
                else if (QueryView.Equals("SubCategory", StringComparison.OrdinalIgnoreCase) ||
                         QueryView.Equals("ItemSubCategory", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "SubCategory";
                }
                else if (QueryView.Equals("Brand", StringComparison.OrdinalIgnoreCase) ||
                         QueryView.Equals("ItemBrand", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Brand";
                }
                else if (QueryView.Equals("Section", StringComparison.OrdinalIgnoreCase) ||
                         QueryView.Equals("ItemGroup", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Section";
                }
                else if (QueryView.Equals("Product", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Product";
                }
                else if (QueryView.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Service";
                }
                else if (QueryView.Equals("Package", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "Package";
                }
                else if (QueryView.Equals("TopUp", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedView = "TopUp";
                }
            }
        }

        private void SelectView(string viewName)
        {
            SelectedView = viewName;

            if (IsSidebarOpen)
                IsSidebarOpen = false;

            if (!IsLeftPanelHidden)
            {
                IsLeftPanelHidden = true;
            }
        }

        private void HideLeftPanel()
        {
            IsLeftPanelHidden = true;
            IsSidebarOpen = false;
        }

        private void ToggleSidebar()
        {
            if (IsLeftPanelHidden)
            {
                IsLeftPanelHidden = false;
                IsSidebarOpen = true;
            }
            else
                IsSidebarOpen = !IsSidebarOpen;
        }

        private void GoToProfile()
        {
            Navigation.NavigateTo("/home");
        }
    }
}