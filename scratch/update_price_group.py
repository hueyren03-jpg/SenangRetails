import re

file_path = r"c:\Users\ebiso\source\repos\SenangRetails\SenangRetails.Shared\Pages\PriceGroupSettings.razor"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. New Modal Markup
new_modal = """<!-- ADD / EDIT PRICE GROUP MODAL (POINT SETUP / STAFF STYLE) -->
@if (showModal)
{
    <div class="modal-overlay" @onclick="CloseModal">
        <div class="modal-box" style="width: 600px; max-width: 95%;" @onclick:stopPropagation>
            <!-- Modal Header -->
            <div class="modal-header">
                <h3 class="modal-title">@(isEditMode ? "Edit Price Group" : "Add Price Group")</h3>
                <button type="button" class="close-btn" @onclick="CloseModal" aria-label="Close">✕</button>
            </div>

            <div class="modal-body">
                @if (!string.IsNullOrEmpty(errorMessage))
                {
                    <div class="form-error">@errorMessage</div>
                }

                <!-- Section Tabs -->
                <div class="staff-tab-bar" style="margin-bottom: 20px;">
                    <button type="button"
                            class="staff-tab @(activeTab == "GeneralSetup" ? "active" : "")"
                            @onclick='() => activeTab = "GeneralSetup"'>
                        General Setup
                    </button>
                    <button type="button"
                            class="staff-tab @(activeTab == "PricingAccess" ? "active" : "")"
                            @onclick='() => activeTab = "PricingAccess"'>
                        Pricing &amp; Access
                    </button>
                    <button type="button"
                            class="staff-tab @(activeTab == "AppliedProducts" ? "active" : "")"
                            @onclick='() => activeTab = "AppliedProducts"'>
                        Applied Products @(currentGroup.AppliedProductIds?.Count > 0 ? $"({currentGroup.AppliedProductIds.Count})" : "")
                    </button>
                </div>

                @if (activeTab == "GeneralSetup")
                {
                    <!-- Price Group Code -->
                    <div class="form-group">
                        <label class="form-label">Price Group Code <span class="required">*</span></label>
                        <input type="text"
                               class="form-control text-uppercase @(isEditMode ? "readonly" : "")"
                               placeholder="e.g. VIP-GOLD"
                               @bind="currentGroup.Code"
                               disabled="@isEditMode" />
                        @if (isEditMode)
                        {
                            <span class="field-sub-hint">Price group code is unique and locked after creation.</span>
                        }
                    </div>

                    <!-- Group Name -->
                    <div class="form-group">
                        <label class="form-label">Group Name <span class="required">*</span></label>
                        <input type="text"
                               class="form-control"
                               placeholder="e.g. VIP Member Gold Tier"
                               @bind="currentGroup.Name" />
                    </div>

                    <!-- Group Selling Price (RM) -->
                    <div class="form-group">
                        <label class="form-label">Group Selling Price (RM) <span class="required">*</span></label>
                        <div class="ps-input-with-action">
                            <input type="number"
                                   step="0.01"
                                   min="0"
                                   class="form-control"
                                   @bind="currentGroup.Price"
                                   placeholder="0.00" />
                            <button type="button"
                                    class="input-keypad-btn"
                                    title="Open touch keypad"
                                    @onclick='() => OpenKeypad("Price", currentGroup.Price, "Selling Price (RM)")'>
                                <i class="fa-solid fa-calculator"></i>
                            </button>
                        </div>
                        <span class="field-sub-hint">Special selling price assigned to all selected items in this group.</span>
                    </div>
                }
                else if (activeTab == "PricingAccess")
                {
                    <!-- MLM Comm Rate & Target side-by-side -->
                    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 18px;">
                        <div class="form-group" style="margin-bottom: 0;">
                            <label class="form-label">MLM Comm Rate (%)</label>
                            <div class="ps-input-with-action">
                                <input type="number"
                                       step="0.01"
                                       min="0"
                                       max="100"
                                       class="form-control"
                                       @bind="currentGroup.MlmCommRate"
                                       placeholder="0.00" />
                                <button type="button"
                                        class="input-keypad-btn"
                                        title="Open touch keypad"
                                        @onclick='() => OpenKeypad("MlmCommRate", currentGroup.MlmCommRate, "MLM Comm Rate (%)")'>
                                    <i class="fa-solid fa-calculator"></i>
                                </button>
                            </div>
                        </div>

                        <div class="form-group" style="margin-bottom: 0;">
                            <label class="form-label">MLM Sales Target (RM)</label>
                            <div class="ps-input-with-action">
                                <input type="number"
                                       step="0.01"
                                       min="0"
                                       class="form-control"
                                       @bind="currentGroup.MlmSalesTarget"
                                       placeholder="0.00" />
                                <button type="button"
                                        class="input-keypad-btn"
                                        title="Open touch keypad"
                                        @onclick='() => OpenKeypad("MlmSalesTarget", currentGroup.MlmSalesTarget, "MLM Target (RM)")'>
                                    <i class="fa-solid fa-calculator"></i>
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Branch Access -->
                    <div class="form-group">
                        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                            <label class="form-label" style="margin: 0;">Branch Access</label>
                            @if (AppState.AvailableBranches != null && AppState.AvailableBranches.Any())
                            {
                                <div class="modal-active-row">
                                    <input type="checkbox"
                                           id="pgSelectAllBranches"
                                           class="staff-checkbox"
                                           checked="@IsAllBranchesSelected()"
                                           @onchange="ToggleSelectAllBranches" />
                                    <label for="pgSelectAllBranches" class="staff-checkbox-label">Select All</label>
                                </div>
                            }
                        </div>

                        @if (AppState.AvailableBranches != null && AppState.AvailableBranches.Any())
                        {
                            <div class="branch-chip-container">
                                @foreach (var branch in AppState.AvailableBranches)
                                {
                                    var isSelected = currentGroup.VisibleBranchIds.Contains(branch);
                                    <button type="button"
                                            class="branch-pill-chip @(isSelected ? "selected" : "")"
                                            @onclick="() => ToggleBranchSelection(branch)">
                                        <i class="fa-solid @(isSelected ? "fa-circle-check" : "fa-circle")"></i>
                                        <span>@branch</span>
                                    </button>
                                }
                            </div>
                        }
                        else
                        {
                            <p style="font-size: 13px; color: #888; margin: 0;">No branches configured in current profile.</p>
                        }
                    </div>
                }
                else if (activeTab == "AppliedProducts")
                {
                    <div class="ps-picker-container">
                        <!-- Top Toolbar: Search + Category -->
                        <div style="display: flex; gap: 8px; margin-bottom: 10px; align-items: center;">
                            <div class="staff-search-wrap" style="flex: 1;">
                                <i class="fa-solid fa-magnifying-glass staff-search-icon"></i>
                                <input type="text"
                                       class="staff-search-input"
                                       placeholder="Search products..."
                                       @bind="prodSearchQuery"
                                       @bind:event="oninput" />
                                @if (!string.IsNullOrEmpty(prodSearchQuery))
                                {
                                    <button type="button" class="staff-search-clear" @onclick='() => prodSearchQuery = ""'>✕</button>
                                }
                            </div>
                            <select class="form-control" style="width: 150px; padding: 10px 12px;" @bind="prodCategoryFilter">
                                <option value="">All Categories</option>
                                @foreach (var cat in availableCategories)
                                {
                                    <option value="@cat">@cat</option>
                                }
                            </select>
                        </div>

                        <!-- Quick Selection Stats & Action Buttons -->
                        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; padding: 4px 2px;">
                            <span style="font-size: 13px; font-weight: 600; color: #555;">
                                <strong style="color: var(--color-primary, #f2600c);">@(currentGroup.AppliedProductIds?.Count ?? 0)</strong> items assigned
                            </span>
                            <div style="display: flex; gap: 8px;">
                                <button type="button" class="btn-picker-action" @onclick="SelectAllFilteredProducts">
                                    Select All (@FilteredProductsForPicker.Count())
                                </button>
                                @if (currentGroup.AppliedProductIds != null && currentGroup.AppliedProductIds.Any())
                                {
                                    <button type="button" class="btn-picker-action danger" @onclick="ClearAllProducts">
                                        Clear All
                                    </button>
                                }
                            </div>
                        </div>

                        <!-- Product List -->
                        <div class="ps-prod-scroll-list">
                            @if (!FilteredProductsForPicker.Any())
                            {
                                <div style="text-align: center; padding: 30px 10px; color: #888;">
                                    <i class="fa-solid fa-box-open" style="font-size: 2rem; color: #ccc; margin-bottom: 8px; display: block;"></i>
                                    <span>No products found matching filter.</span>
                                </div>
                            }
                            else
                            {
                                @foreach (var prod in FilteredProductsForPicker)
                                {
                                    var isChecked = !string.IsNullOrEmpty(prod.MasterAccountID) && currentGroup.AppliedProductIds.Any(id => string.Equals(id?.Trim(), prod.MasterAccountID.Trim(), StringComparison.OrdinalIgnoreCase));
                                    var imgUrl = GetDisplayImageUrl(prod);

                                    <div class="ps-prod-item @(isChecked ? "selected" : "")"
                                         @onclick="() => ToggleProductSelection(prod.MasterAccountID ?? string.Empty)">
                                        <div class="custom-tick-checkbox @(isChecked ? "checked" : "")" style="flex-shrink: 0;">
                                            @if (isChecked)
                                            {
                                                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                                            }
                                        </div>

                                        @if (!string.IsNullOrEmpty(imgUrl) && !failedImageItemIds.Contains(prod.MasterAccountID ?? ""))
                                        {
                                            <img src="@imgUrl"
                                                 alt="@prod.AccountName"
                                                 class="ps-prod-img"
                                                 loading="lazy"
                                                 @onerror="() => failedImageItemIds.Add(prod.MasterAccountID ?? string.Empty)" />
                                        }
                                        else
                                        {
                                            <div class="ps-prod-img-placeholder">
                                                <i class="fa-solid fa-box"></i>
                                            </div>
                                        }

                                        <div style="flex: 1; min-width: 0;">
                                            <div style="font-size: 13.5px; font-weight: 600; color: #222; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
                                                @prod.AccountName
                                            </div>
                                            <div style="display: flex; gap: 8px; align-items: center; margin-top: 2px;">
                                                @if (!string.IsNullOrEmpty(prod.ItemGroupName))
                                                {
                                                    <span class="ps-badge-tag">@prod.ItemGroupName</span>
                                                }
                                                <span style="font-size: 12px; color: #888;">Orig: RM @prod.SalesPrice.ToString("N2")</span>
                                                @if (isChecked && currentGroup.Price > 0)
                                                {
                                                    <span style="font-size: 12px; font-weight: 600; color: #16a34a;">➔ RM @currentGroup.Price.ToString("N2")</span>
                                                }
                                            </div>
                                        </div>
                                    </div>
                                }
                            }
                        </div>
                    </div>
                }

                @if (isEditMode)
                {
                    <div style="margin-top: 14px; padding-top: 14px; border-top: 1px solid #f0f0f0;">
                        <button type="button" class="btn-delete-row" @onclick="() => DeleteGroup(currentGroup.Code)">
                            <i class="fa-solid fa-trash-can" style="margin-right: 6px;"></i> Delete Price Group
                        </button>
                    </div>
                }
            </div>

            <!-- Modal Footer (Matching Staff & Point Setup style) -->
            <div class="modal-footer">
                <button type="button" class="btn-cancel" @onclick="CloseModal">Cancel</button>
                <button type="button" class="btn-save" @onclick="SaveGroup" disabled="@isSaving">
                    @(isSaving ? "Saving..." : "Save")
                </button>
            </div>
        </div>
    </div>
}

<!-- TOUCH CALCULATOR KEYPAD MODAL -->
@if (showKeypad)
{
    <div class="modal-overlay" @onclick="CloseKeypad">
        <div class="touch-keypad-modal" @onclick:stopPropagation>
            <div class="keypad-header">
                <span class="keypad-title">Enter @keypadTitle</span>
                <span class="keypad-display">@keypadValueString</span>
            </div>
            <div class="keypad-grid">
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("7")'>7</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("8")'>8</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("9")'>9</button>
                <button type="button" class="kp-btn kp-action" @onclick="KeypadClear">CE</button>

                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("4")'>4</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("5")'>5</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("6")'>6</button>
                <button type="button" class="kp-btn kp-action" @onclick="KeypadBackspace">⌫</button>

                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("1")'>1</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("2")'>2</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("3")'>3</button>
                <button type="button" class="kp-btn kp-cancel" @onclick="CloseKeypad">Cancel</button>

                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("0")'>0</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend("00")'>00</button>
                <button type="button" class="kp-btn" @onclick='() => KeypadAppend(".")'>.</button>
                <button type="button" class="kp-btn kp-submit" @onclick="KeypadSave">OK</button>
            </div>
        </div>
    </div>
}"""

# Replace old modal markup
old_modal_pattern = re.compile(r'<!-- ADD / EDIT MODAL -->.*?<!-- TOUCH CALCULATOR KEYPAD MODAL -->.*?</style>', re.DOTALL)

# 2. New Clean CSS for modal and components
new_css = """    /* =========================================================================
       CLEAN MODAL & FORM SYSTEM (POINT SETUP / STAFF STANDARDS)
       ========================================================================= */
    .modal-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.45);
        backdrop-filter: blur(2px);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1050;
        padding: 16px;
    }

    .modal-box {
        background: white;
        border-radius: 16px;
        width: 600px;
        max-width: 95%;
        max-height: 90vh;
        max-height: 90dvh;
        display: flex;
        flex-direction: column;
        box-shadow: 0 20px 50px rgba(0, 0, 0, 0.15);
        overflow: hidden;
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    }

    .modal-header {
        padding: 20px 24px 16px;
        display: flex;
        justify-content: space-between;
        align-items: center;
        border-bottom: 1px solid #f0f0f0;
        flex-shrink: 0;
    }

    .modal-title {
        font-size: 18px;
        font-weight: 700;
        color: #1a1a1a;
        margin: 0;
    }

    .close-btn {
        background: none;
        border: none;
        font-size: 20px;
        color: #888;
        cursor: pointer;
        padding: 4px 8px;
        border-radius: 6px;
        line-height: 1;
        transition: 0.15s;
    }

    .close-btn:hover {
        background: #f0f0f0;
        color: #111;
    }

    .modal-body {
        padding: 20px 24px;
        overflow-y: auto;
        flex: 1;
        box-sizing: border-box;
    }

    .modal-footer {
        padding: 16px 24px;
        display: flex;
        gap: 12px;
        border-top: 1px solid #f0f0f0;
        flex-shrink: 0;
    }

    .btn-cancel {
        flex: 1;
        padding: 13px;
        border: 1px solid #ddd;
        border-radius: 10px;
        background: white;
        font-size: 15px;
        font-weight: 600;
        color: #444;
        cursor: pointer;
        transition: 0.2s;
        font-family: inherit;
        text-align: center;
    }

    .btn-cancel:hover {
        background: #f5f5f5;
    }

    .btn-save {
        flex: 1;
        padding: 13px;
        border: none;
        border-radius: 10px;
        background-color: var(--color-primary, #F2600C);
        color: white;
        font-size: 15px;
        font-weight: 600;
        cursor: pointer;
        transition: opacity 0.2s;
        font-family: inherit;
        text-align: center;
    }

    .btn-save:hover {
        opacity: 0.88;
    }

    .btn-save:disabled {
        opacity: 0.5;
        cursor: not-allowed;
    }

    /* Form controls */
    .form-group {
        margin-bottom: 18px;
    }

    .form-group:last-child {
        margin-bottom: 0;
    }

    .form-label {
        display: block;
        font-size: 13px;
        font-weight: 600;
        color: #333;
        margin-bottom: 8px;
    }

    .required {
        color: var(--color-primary, #F2600C);
    }

    .form-control {
        width: 100%;
        padding: 12px 14px;
        border: 1px solid #ddd;
        border-radius: 10px;
        font-size: 14px;
        font-family: inherit;
        box-sizing: border-box;
        color: #222;
        transition: border-color 0.2s;
        background: white;
    }

    .form-control:focus {
        outline: none;
        border-color: var(--color-primary, #F2600C);
    }

    .form-control.readonly {
        background: #f5f5f5;
        color: #888;
        cursor: not-allowed;
    }

    .field-sub-hint {
        display: block;
        font-size: 12px;
        color: #888;
        margin-top: 4px;
    }

    .form-error {
        background: #fff0f0;
        border: 1px solid #ffcdd2;
        color: #c62828;
        padding: 10px 14px;
        border-radius: 8px;
        font-size: 13px;
        margin-bottom: 16px;
    }

    /* Active Row & Checkbox */
    .modal-active-row {
        display: flex;
        align-items: center;
        gap: 8px;
    }

    .staff-checkbox {
        appearance: none;
        -webkit-appearance: none;
        width: 18px;
        height: 18px;
        border: 1.5px solid #ccc;
        border-radius: 5px;
        background-color: white;
        cursor: pointer;
        position: relative;
        flex-shrink: 0;
        transition: border-color 0.2s;
        margin: 0;
    }

    .staff-checkbox:checked {
        background-color: var(--color-primary, #F2600C);
        border-color: var(--color-primary, #F2600C);
    }

    .staff-checkbox:checked::after {
        content: '';
        position: absolute;
        left: 5px;
        top: 1px;
        width: 5px;
        height: 10px;
        border: solid white;
        border-width: 0 2px 2px 0;
        transform: rotate(45deg);
    }

    .staff-checkbox-label {
        font-size: 13px;
        font-weight: 600;
        color: #555;
        cursor: pointer;
        margin: 0;
    }

    /* Staff Tab Bar (Pills) */
    .staff-tab-bar {
        display: flex;
        gap: 8px;
    }

    .staff-tab {
        padding: 8px 18px;
        border: 1px solid #ddd;
        background: white;
        border-radius: 20px;
        font-size: 13.5px;
        font-weight: 500;
        cursor: pointer;
        transition: all 0.2s;
        color: #666;
        font-family: inherit;
    }

    .staff-tab:hover:not(.active) {
        background: #f5f5f5;
        border-color: var(--color-primary, #F2600C);
    }

    .staff-tab.active {
        background: var(--color-primary, #F2600C);
        color: white;
        border-color: var(--color-primary, #F2600C);
    }

    /* Input with Keypad button */
    .ps-input-with-action {
        position: relative;
        display: flex;
        align-items: center;
        width: 100%;
    }

    .ps-input-with-action .form-control {
        padding-right: 46px;
    }

    .input-keypad-btn {
        position: absolute;
        right: 8px;
        background: #f5f5f5;
        border: 1px solid #e0e0e0;
        border-radius: 6px;
        width: 32px;
        height: 32px;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #666;
        cursor: pointer;
        transition: all 0.15s;
    }

    .input-keypad-btn:hover {
        background: #eee;
        color: var(--color-primary, #F2600C);
        border-color: var(--color-primary, #F2600C);
    }

    /* Branch Chip Selector */
    .branch-chip-container {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        margin-top: 4px;
    }

    .branch-pill-chip {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 7px 14px;
        border-radius: 8px;
        border: 1px solid #ddd;
        background: white;
        font-size: 13px;
        font-weight: 600;
        color: #555;
        cursor: pointer;
        transition: all 0.15s ease;
        font-family: inherit;
    }

    .branch-pill-chip:hover {
        background: #f8fafc;
        border-color: #cbd5e1;
    }

    .branch-pill-chip.selected {
        background: #FFF7ED;
        border-color: var(--color-primary, #F2600C);
        color: var(--color-primary, #F2600C);
    }

    /* Applied Products Tab Styles */
    .ps-picker-container {
        display: flex;
        flex-direction: column;
    }

    .staff-search-clear {
        position: absolute;
        right: 10px;
        top: 50%;
        transform: translateY(-50%);
        background: none;
        border: none;
        color: #888;
        font-size: 13px;
        cursor: pointer;
        padding: 2px 4px;
        line-height: 1;
    }

    .btn-picker-action {
        padding: 5px 12px;
        font-size: 12px;
        font-weight: 600;
        border-radius: 6px;
        border: 1px solid #ddd;
        background: white;
        color: #444;
        cursor: pointer;
        transition: 0.15s;
    }

    .btn-picker-action:hover {
        background: #f5f5f5;
        border-color: #ccc;
    }

    .btn-picker-action.danger {
        color: #d32f2f;
        border-color: #ffcdd2;
        background: #fff5f5;
    }

    .btn-picker-action.danger:hover {
        background: #ffebee;
    }

    .ps-prod-scroll-list {
        max-height: 340px;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 8px;
        padding-right: 4px;
    }

    .ps-prod-item {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 10px 12px;
        border: 1px solid #eee;
        border-radius: 10px;
        background: white;
        cursor: pointer;
        transition: all 0.15s;
    }

    .ps-prod-item:hover {
        border-color: #ddd;
        background: #fafafa;
    }

    .ps-prod-item.selected {
        border-color: var(--color-primary, #F2600C);
        background: #FFFBF7;
    }

    .custom-tick-checkbox {
        width: 18px;
        height: 18px;
        border: 1.5px solid #ccc;
        border-radius: 4px;
        background-color: white;
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        transition: all 0.15s ease;
    }

    .custom-tick-checkbox.checked {
        background-color: var(--color-primary, #F2600C);
        border-color: var(--color-primary, #F2600C);
    }

    .ps-prod-img {
        width: 40px;
        height: 40px;
        border-radius: 8px;
        object-fit: cover;
        flex-shrink: 0;
    }

    .ps-prod-img-placeholder {
        width: 40px;
        height: 40px;
        border-radius: 8px;
        background: #f5f5f5;
        color: #bbb;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        font-size: 16px;
    }

    .ps-badge-tag {
        font-size: 11px;
        background: #f0f0f0;
        color: #666;
        padding: 2px 7px;
        border-radius: 4px;
        font-weight: 500;
    }

    .btn-delete-row {
        width: 100%;
        padding: 10px;
        border: 1.5px solid #ffcdd2;
        border-radius: 10px;
        background: #fff5f5;
        color: #d32f2f;
        font-size: 13.5px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.2s;
        display: flex;
        align-items: center;
        justify-content: center;
        font-family: inherit;
    }

    .btn-delete-row:hover {
        background: #ffebee;
        border-color: #ef5350;
    }

    /* Touch Keypad */
    .touch-keypad-modal {
        background: white;
        border-radius: 20px;
        padding: 20px;
        width: 320px;
        box-shadow: 0 20px 40px rgba(0, 0, 0, 0.2);
    }

    .keypad-header {
        margin-bottom: 16px;
        text-align: center;
    }

    .keypad-title {
        display: block;
        font-size: 13px;
        color: #888;
        margin-bottom: 4px;
    }

    .keypad-display {
        font-size: 28px;
        font-weight: 700;
        color: #222;
    }

    .keypad-grid {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 10px;
    }

    .kp-btn {
        padding: 14px 0;
        font-size: 18px;
        font-weight: 600;
        border: 1px solid #eee;
        border-radius: 12px;
        background: #fcfcfc;
        color: #333;
        cursor: pointer;
        transition: all 0.15s;
    }

    .kp-btn:active {
        transform: scale(0.96);
    }

    .kp-action {
        background: #f5f5f5;
        color: #666;
    }

    .kp-cancel {
        background: #fff0f0;
        color: #d32f2f;
        border-color: #ffcdd2;
        font-size: 13px;
    }

    .kp-submit {
        background: var(--color-primary, #F2600C);
        color: white;
        border-color: var(--color-primary, #F2600C);
    }
</style>"""

# Find bounds of old markup and replace
modal_start = content.find("<!-- ADD / EDIT MODAL -->")
modal_end = content.find("<style>", modal_start)
if modal_start != -1 and modal_end != -1:
    content = content[:modal_start] + new_modal + "\n\n" + content[modal_end:]
    print("Replaced modal markup successfully")
else:
    print("Could not find modal markup boundaries")

# Find bounds of old modal css in style tag
css_start = content.find("/* ══════════════════════════════════════════════════\n       BOUNDED MODAL & FORM SYSTEM")
if css_start == -1:
    css_start = content.find(".pg-modal-overlay {")
css_end = content.find("</style>", css_start)
if css_start != -1 and css_end != -1:
    content = content[:css_start] + new_css.strip() + "\n" + content[css_end + 8:]
    print("Replaced CSS successfully")
else:
    print("Could not find CSS boundaries")

# Update @code block
# 1. Add activeTab and failedImageItemIds
if "private string activeTab" not in content:
    content = content.replace("private bool isSaving = false;", "private bool isSaving = false;\n    private string activeTab = \"GeneralSetup\";\n    private readonly HashSet<string> failedImageItemIds = new();")

# 2. Reset in OpenAddModal & OpenEditModal
content = content.replace('errorMessage = "";\n        prodSearchQuery = "";', 'errorMessage = "";\n        activeTab = "GeneralSetup";\n        failedImageItemIds.Clear();\n        prodSearchQuery = "";')

# 3. Update GetDisplayImageUrl
old_get_display = """    private string GetDisplayImageUrl(InventoryDM item)
    {
        return CacheService.GetImage(item.MasterAccountID ?? "")
            ?? LocalImageCache.GetImage(item.MasterAccountID ?? "")
            ?? ((!string.IsNullOrEmpty(item.ImagePath) && !string.IsNullOrEmpty(item.ImageFileName))
                ? $"{item.ImagePath}/{item.ImageFileName}" : "");
    }"""

new_get_display = """    private string GetDisplayImageUrl(InventoryDM item)
    {
        if (item == null) return string.Empty;
        var masterId = item.MasterAccountID?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(masterId))
        {
            var cached = CacheService.GetImage(masterId) ?? LocalImageCache.GetImage(masterId);
            if (!string.IsNullOrEmpty(cached)) return cached;
        }
        return ProductCacheService.ResolveServerImageUrl(item.ImagePath, item.ImageFileName);
    }"""

if old_get_display in content:
    content = content.replace(old_get_display, new_get_display)
    print("Replaced GetDisplayImageUrl successfully")
else:
    print("GetDisplayImageUrl exact match not found, checking...")

# 4. In DeleteGroup, add showModal = false;
content = content.replace("CacheService.TriggerBackgroundRefresh();\n            await LoadDataAsync();", "CacheService.TriggerBackgroundRefresh();\n            showModal = false;\n            await LoadDataAsync();")

# 5. In LoadDataAsync fallback, pass AppState.SelectedBranchID
content = content.replace('var liveItems = await InventorySvc.LoadItemsAsync("");', 'var liveItems = await InventorySvc.LoadItemsAsync(AppState.SelectedBranchID ?? "");')

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Saved updated PriceGroupSettings.razor")
