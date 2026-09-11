import re

file_path = r"c:\Users\ebiso\source\repos\SenangRetails\SenangRetails.Shared\Pages\BarcodeSetupSettings.razor"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. New Modal Markup
new_modal = """<!-- TOUCH ADD / EDIT MODAL (POINT SETUP / STAFF STYLE) -->
@if (showModal)
{
    <div class="modal-overlay" @onclick="CloseModal">
        <div class="modal-box" style="width: 580px; max-width: 95%;" @onclick:stopPropagation>
            <!-- STICKY MODAL HEADER -->
            <div class="modal-header">
                <h3 class="modal-title">@(isEditMode ? "Edit Barcode Rule" : "Add Barcode Rule")</h3>
                <button type="button" class="close-btn" @onclick="CloseModal" aria-label="Close">✕</button>
            </div>

            <!-- MODAL BODY (SCROLLABLE & BOUNDED) -->
            <div class="modal-body">
                @if (!string.IsNullOrEmpty(errorMessage))
                {
                    <div class="form-error">@errorMessage</div>
                }

                <!-- Section Tabs -->
                <div class="staff-tab-bar" style="margin-bottom: 18px;">
                    <button type="button"
                            class="staff-tab @(activeTab == "RulePattern" ? "active" : "")"
                            @onclick='() => activeTab = "RulePattern"'>
                        Rule &amp; Lengths
                    </button>
                    <button type="button"
                            class="staff-tab @(activeTab == "ScannerFlags" ? "active" : "")"
                            @onclick='() => activeTab = "ScannerFlags"'>
                        Scanner Flags
                    </button>
                </div>

                @if (activeTab == "RulePattern")
                {
                    <!-- LIVE PARSING VISUAL SIMULATOR -->
                    <div class="bc-live-preview-box">
                        <div class="bc-preview-title">
                            <i class="fa-solid fa-wand-magic-sparkles"></i>
                            <span>Live Barcode Parsing Preview</span>
                        </div>
                        <div class="bc-preview-strip">
                            <span class="bc-p-seg pre" title="Prefix">@(string.IsNullOrEmpty(currentRule.Prefix) ? "21" : currentRule.Prefix)</span>
                            <span class="bc-p-seg item" title="Item Code">@(new string('X', Math.Max(1, currentRule.ItemCodeLength)))</span>
                            @if (!currentRule.IsQuantityFixedAsOne && currentRule.QuantityLength > 0)
                            {
                                <span class="bc-p-seg qty" title="Quantity">@(new string('Q', currentRule.QuantityLength))</span>
                            }
                            @if (currentRule.PriceLength > 0)
                            {
                                <span class="bc-p-seg price" title="Price">@(new string('P', currentRule.PriceLength))</span>
                            }
                            @if (currentRule.ChecksumLength > 0)
                            {
                                <span class="bc-p-seg chk" title="Checksum">@(new string('C', currentRule.ChecksumLength))</span>
                            }
                        </div>
                        <div class="bc-preview-legend">
                            <span class="leg-item"><span class="leg-dot pre"></span> Prefix (@(currentRule.Prefix?.Length ?? 2))</span>
                            <span class="leg-item"><span class="leg-dot item"></span> Item (@currentRule.ItemCodeLength)</span>
                            @if (!currentRule.IsQuantityFixedAsOne)
                            {
                                <span class="leg-item"><span class="leg-dot qty"></span> Qty (@currentRule.QuantityLength)</span>
                            }
                            <span class="leg-item"><span class="leg-dot price"></span> Price (@currentRule.PriceLength, .@currentRule.DecimalPlace)</span>
                            @if (currentRule.ChecksumLength > 0)
                            {
                                <span class="leg-item"><span class="leg-dot chk"></span> Checksum (@currentRule.ChecksumLength)</span>
                            }
                        </div>
                    </div>

                    <!-- Prefix & Barcode Type side-by-side -->
                    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px;">
                        <div class="form-group">
                            <label class="form-label">Prefix (2 Chars) <span class="required">*</span></label>
                            <input type="text"
                                   maxlength="2"
                                   class="form-control text-uppercase text-mono @(isEditMode ? "readonly" : "")"
                                   placeholder="e.g. 21"
                                   @bind="currentRule.Prefix"
                                   disabled="@isEditMode" />
                            @if (isEditMode)
                            {
                                <span class="field-sub-hint">Prefix is locked after creation.</span>
                            }
                        </div>

                        <div class="form-group">
                            <label class="form-label">Barcode Type</label>
                            <select class="form-control" @bind="currentRule.BarcodeType">
                                <option value="BarCode">BarCode (Standard)</option>
                                <option value="EAN-13">EAN-13 (Standard Retail)</option>
                                <option value="Code-128">Code-128</option>
                                <option value="UPC-A">UPC-A</option>
                                <option value="Custom Embedded">Custom Embedded Barcode</option>
                            </select>
                        </div>
                    </div>

                    <!-- Format Selection (Barcode / QRCode) -->
                    <div class="form-group">
                        <label class="form-label">Format Selection</label>
                        <div class="touch-pill-group">
                            <button type="button"
                                    class="touch-pill @(currentRule.SelectionType == "Barcode" ? "active" : "")"
                                    @onclick='() => currentRule.SelectionType = "Barcode"'>
                                <i class="fa-solid fa-barcode"></i> Barcode
                            </button>
                            <button type="button"
                                    class="touch-pill @(currentRule.SelectionType == "QRCode" ? "active" : "")"
                                    @onclick='() => currentRule.SelectionType = "QRCode"'>
                                <i class="fa-solid fa-qrcode"></i> QRCode
                            </button>
                        </div>
                    </div>

                    <!-- Item Code Length -->
                    <div class="form-group">
                        <label class="form-label">Item Code Length <span class="required">*</span></label>
                        <div class="ps-input-with-action">
                            <input type="number"
                                   min="1"
                                   max="20"
                                   class="form-control"
                                   @bind="currentRule.ItemCodeLength"
                                   placeholder="5" />
                            <button type="button"
                                    class="input-keypad-btn"
                                    title="Open touch keypad"
                                    @onclick='() => OpenKeypad("ItemCodeLength", currentRule.ItemCodeLength, "Item Code Length")'>
                                <i class="fa-solid fa-calculator"></i>
                            </button>
                        </div>
                    </div>

                    <!-- Quantity Mode & Length -->
                    <div class="form-group">
                        <label class="form-label">Quantity Mode</label>
                        <div class="touch-pill-group">
                            <button type="button"
                                    class="touch-pill @(!currentRule.IsQuantityFixedAsOne ? "active" : "")"
                                    @onclick="() => currentRule.IsQuantityFixedAsOne = false">
                                <i class="fa-solid fa-arrow-down-9-1"></i> Extract Qty
                            </button>
                            <button type="button"
                                    class="touch-pill @(currentRule.IsQuantityFixedAsOne ? "active" : "")"
                                    @onclick="() => currentRule.IsQuantityFixedAsOne = true">
                                <i class="fa-solid fa-1"></i> Fixed as 1
                            </button>
                        </div>
                    </div>

                    @if (!currentRule.IsQuantityFixedAsOne)
                    {
                        <div class="form-group">
                            <label class="form-label">Quantity Length (Digits) <span class="required">*</span></label>
                            <div class="ps-input-with-action">
                                <input type="number"
                                       min="1"
                                       max="10"
                                       class="form-control"
                                       @bind="currentRule.QuantityLength"
                                       placeholder="5" />
                                <button type="button"
                                        class="input-keypad-btn"
                                        title="Open touch keypad"
                                        @onclick='() => OpenKeypad("QuantityLength", currentRule.QuantityLength, "Quantity Length")'>
                                    <i class="fa-solid fa-calculator"></i>
                                </button>
                            </div>
                        </div>
                    }

                    <!-- Price Length & Decimal Place side-by-side -->
                    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px;">
                        <div class="form-group">
                            <label class="form-label">Price Length (Digits)</label>
                            <div class="ps-input-with-action">
                                <input type="number"
                                       min="0"
                                       max="10"
                                       class="form-control"
                                       @bind="currentRule.PriceLength"
                                       placeholder="5" />
                                <button type="button"
                                        class="input-keypad-btn"
                                        title="Open touch keypad"
                                        @onclick='() => OpenKeypad("PriceLength", currentRule.PriceLength, "Price Length")'>
                                    <i class="fa-solid fa-calculator"></i>
                                </button>
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label">Decimal Places</label>
                            <div class="ps-input-with-action">
                                <input type="number"
                                       min="0"
                                       max="6"
                                       class="form-control"
                                       @bind="currentRule.DecimalPlace"
                                       placeholder="2" />
                                <button type="button"
                                        class="input-keypad-btn"
                                        title="Open touch keypad"
                                        @onclick='() => OpenKeypad("DecimalPlace", currentRule.DecimalPlace, "Decimal Place")'>
                                    <i class="fa-solid fa-calculator"></i>
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Checksum Length -->
                    <div class="form-group">
                        <label class="form-label">Checksum Length (Digits)</label>
                        <div class="ps-input-with-action">
                            <input type="number"
                                   min="0"
                                   max="5"
                                   class="form-control"
                                   @bind="currentRule.ChecksumLength"
                                   placeholder="1" />
                            <button type="button"
                                    class="input-keypad-btn"
                                    title="Open touch keypad"
                                    @onclick='() => OpenKeypad("ChecksumLength", currentRule.ChecksumLength, "Checksum Length")'>
                                <i class="fa-solid fa-calculator"></i>
                            </button>
                        </div>
                    </div>
                }
                else if (activeTab == "ScannerFlags")
                {
                    <div class="bc-toggle-card @(currentRule.IsActive ? "checked" : "")"
                         @onclick="() => currentRule.IsActive = !currentRule.IsActive">
                        <div class="bc-toggle-left">
                            <i class="fa-solid fa-power-off"></i>
                            <div>
                                <span class="bc-toggle-title">Active Rule</span>
                                <span class="bc-toggle-desc">Enable this parsing rule for all active POS scanner operations.</span>
                            </div>
                        </div>
                        <div class="custom-tick-checkbox @(currentRule.IsActive ? "checked" : "")" style="flex-shrink: 0;">
                            @if (currentRule.IsActive)
                            {
                                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                            }
                        </div>
                    </div>

                    <div class="bc-toggle-card @(currentRule.BarcodeIncludesPrefix ? "checked" : "")"
                         @onclick="() => currentRule.BarcodeIncludesPrefix = !currentRule.BarcodeIncludesPrefix">
                        <div class="bc-toggle-left">
                            <i class="fa-solid fa-indent"></i>
                            <div>
                                <span class="bc-toggle-title">Barcode Includes Prefix</span>
                                <span class="bc-toggle-desc">Include the 2-digit prefix when matching the item code in inventory.</span>
                            </div>
                        </div>
                        <div class="custom-tick-checkbox @(currentRule.BarcodeIncludesPrefix ? "checked" : "")" style="flex-shrink: 0;">
                            @if (currentRule.BarcodeIncludesPrefix)
                            {
                                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                            }
                        </div>
                    </div>

                    <div class="bc-toggle-card @(currentRule.HasOverlappingBarcodeItem ? "checked" : "")"
                         @onclick="() => currentRule.HasOverlappingBarcodeItem = !currentRule.HasOverlappingBarcodeItem">
                        <div class="bc-toggle-left">
                            <i class="fa-solid fa-clone"></i>
                            <div>
                                <span class="bc-toggle-title">Has Overlapping Barcode Item</span>
                                <span class="bc-toggle-desc">Support items sharing overlapping sub-barcode series.</span>
                            </div>
                        </div>
                        <div class="custom-tick-checkbox @(currentRule.HasOverlappingBarcodeItem ? "checked" : "")" style="flex-shrink: 0;">
                            @if (currentRule.HasOverlappingBarcodeItem)
                            {
                                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                            }
                        </div>
                    </div>

                    @if (isEditMode)
                    {
                        <div style="margin-top: 18px; padding-top: 14px; border-top: 1px solid #f0f0f0;">
                            <button type="button" class="btn-delete-row" @onclick="() => ConfirmDelete(currentRule)">
                                <i class="fa-solid fa-trash-can" style="margin-right: 6px;"></i> Delete Barcode Rule
                            </button>
                        </div>
                    }
                }
            </div>

            <!-- MODAL FOOTER (MATCHING STAFF & POINT SETUP STYLE) -->
            <div class="modal-footer">
                <button type="button" class="btn-cancel" @onclick="CloseModal">Cancel</button>
                <button type="button" class="btn-save" @onclick="SaveRule" disabled="@isSaving">
                    @(isSaving ? "Saving..." : "Save")
                </button>
            </div>
        </div>
    </div>
}"""

# 2. Replace Modal Markup
modal_start = content.find("<!-- TOUCH ADD / EDIT MODAL -->")
modal_end = content.find("<!-- TOUCH CALCULATOR KEYPAD MODAL -->")
if modal_start != -1 and modal_end != -1:
    content = content[:modal_start] + new_modal + "\n\n" + content[modal_end:]
    print("Replaced modal markup successfully")
else:
    print("Could not find modal markup boundaries")

# 3. New Clean CSS for modal and components
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
        width: 580px;
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

    /* Touch Pills */
    .touch-pill-group {
        display: flex;
        gap: 8px;
    }

    .touch-pill {
        flex: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        padding: 9px 14px;
        border-radius: 10px;
        border: 1px solid #ddd;
        background: white;
        font-size: 13.5px;
        font-weight: 500;
        color: #555;
        cursor: pointer;
        transition: all 0.15s;
        font-family: inherit;
    }

    .touch-pill:hover {
        background: #f9f9f9;
        border-color: #ccc;
    }

    .touch-pill.active {
        background: #FFF7ED;
        border-color: var(--color-primary, #F2600C);
        color: var(--color-primary, #F2600C);
        font-weight: 600;
    }

    /* Live Preview Simulator */
    .bc-live-preview-box {
        background: #0F172A;
        border-radius: 12px;
        padding: 14px 16px;
        color: white;
        margin-bottom: 18px;
    }

    .bc-preview-title {
        font-size: 0.75rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #94A3B8;
        margin-bottom: 8px;
        display: flex;
        align-items: center;
        gap: 6px;
        font-weight: 600;
    }

    .bc-preview-strip {
        display: flex;
        gap: 4px;
        font-family: monospace;
        font-size: 1.1rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        margin-bottom: 10px;
        overflow-x: auto;
    }

    .bc-p-seg {
        padding: 4px 8px;
        border-radius: 6px;
    }
    .bc-p-seg.pre { background: #3B82F6; color: white; }
    .bc-p-seg.item { background: #10B981; color: white; }
    .bc-p-seg.qty { background: #F59E0B; color: white; }
    .bc-p-seg.price { background: #EF4444; color: white; }
    .bc-p-seg.chk { background: #8B5CF6; color: white; }

    .bc-preview-legend {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        font-size: 0.75rem;
        color: #CBD5E1;
    }

    .leg-item {
        display: flex;
        align-items: center;
        gap: 5px;
    }

    .leg-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
    }
    .leg-dot.pre { background: #3B82F6; }
    .leg-dot.item { background: #10B981; }
    .leg-dot.qty { background: #F59E0B; }
    .leg-dot.price { background: #EF4444; }
    .leg-dot.chk { background: #8B5CF6; }

    /* Scanner Toggle Cards */
    .bc-toggle-card {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 14px 16px;
        background: white;
        border: 1px solid #eee;
        border-radius: 12px;
        cursor: pointer;
        transition: all 0.15s;
        margin-bottom: 12px;
    }

    .bc-toggle-card:hover {
        border-color: #ddd;
        background: #fafafa;
    }

    .bc-toggle-card.checked {
        border-color: var(--color-primary, #F2600C);
        background: #FFFBF7;
    }

    .bc-toggle-left {
        display: flex;
        align-items: center;
        gap: 12px;
    }

    .bc-toggle-left i {
        font-size: 18px;
        color: #888;
        width: 24px;
        text-align: center;
    }

    .bc-toggle-card.checked .bc-toggle-left i {
        color: var(--color-primary, #F2600C);
    }

    .bc-toggle-title {
        display: block;
        font-weight: 600;
        font-size: 14px;
        color: #222;
        margin-bottom: 2px;
    }

    .bc-toggle-desc {
        display: block;
        font-size: 12px;
        color: #888;
        line-height: 1.3;
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

    /* Keypad Modal */
    .touch-keypad-modal {
        background: white;
        border-radius: 20px;
        width: 320px;
        padding: 20px;
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

    /* Delete Confirm Modal */
    .bc-del-modal {
        max-width: 440px;
    }

    .bc-del-header-left {
        display: flex;
        align-items: center;
        gap: 12px;
    }

    .bc-del-icon-badge {
        width: 38px;
        height: 38px;
        border-radius: 10px;
        background: #FEF2F2;
        color: #DC2626;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.1rem;
    }"""

# Replace old CSS in style tag
old_css_start = content.find(".modal-container {")
old_css_end = content.find("/* ══════════════════════════════════════════════════\n       RESPONSIVE MOBILE CARDS")
if old_css_start != -1 and old_css_end != -1:
    content = content[:old_css_start] + new_css.strip() + "\n\n    " + content[old_css_end:]
    print("Replaced CSS successfully")
else:
    print("Could not find CSS boundaries")

# Update @code
if "private string activeTab" not in content:
    content = content.replace("private string errorMessage = \"\";", "private string errorMessage = \"\";\n    private string activeTab = \"RulePattern\";")

content = content.replace('isEditMode = false;\n        errorMessage = "";', 'isEditMode = false;\n        errorMessage = "";\n        activeTab = "RulePattern";')
content = content.replace('isEditMode = true;\n        errorMessage = "";', 'isEditMode = true;\n        errorMessage = "";\n        activeTab = "RulePattern";')

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Saved updated BarcodeSetupSettings.razor")
