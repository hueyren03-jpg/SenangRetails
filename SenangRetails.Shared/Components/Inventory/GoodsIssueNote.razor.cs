using EBI.DM;
using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.InventoryService;

namespace SenangRetails.Shared.Components.Inventory
{
    public partial class GoodsIssueNote
    {
        [Inject] private IInventoryService InventoryService { get; set; } = default!;
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;

        [Parameter] public EventCallback OnToggleNav { get; set; }
        
            private readonly string[] activityOptions =
            [
                "Internal Use",
                "Broken",
                "Expired",
                "Missing",
                "Sample / Tester",
                "Gift / Present",
                "Stock Adjustment"
            ];
        
            private List<GinDocumentSummary> documents = [];
            private List<InventoryDM> availableItems = [];
            private readonly List<DocumentLineTableDM> deletedServerLines = [];
        
            private GinDraft draft = new();
            private GinDocumentDto? loadedGin;
            private bool isEditorOpen;
            private bool isEditingExisting;
            private bool showItemPicker;
            private bool activityMenuOpen;
            private bool isSaving;
            private bool isLoadingDocuments;
            private string searchText = string.Empty;
            private string statusFilter = "All";
            private DateTime filterFrom = DateTime.Today.AddMonths(-1);
            private DateTime filterTo = DateTime.Today;
            private string messageText = string.Empty;
            private string messageType = "success";
        
            private IEnumerable<GinDocumentSummary> FilteredDocuments => documents
                .Where(item => item.FinancialDate.Date >= filterFrom.Date && item.FinancialDate.Date <= filterTo.Date)
                .Where(item => statusFilter == "All" || item.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(searchText)
                    || item.DocumentNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.ReferenceNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.Activity.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.FinancialDate);
        
            protected override async Task OnInitializedAsync()
            {
                var branchId = ResolveBranchId();
                draft = NewDraft(branchId);
        
                await LoadItemsAsync();
                await LoadDocumentsAsync();
            }
        
            private string ResolveBranchId() =>
                string.IsNullOrWhiteSpace(AppState.SelectedBranchID)
                    ? AppState.CurrentBranch?.BranchID ?? "HQ"
                    : AppState.SelectedBranchID;
        
            private async Task LoadItemsAsync()
            {
                try
                {
                    availableItems = await InventoryService.LoadItemsAsync(ResolveBranchId()) ?? [];
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[GoodsIssueNote] Unable to load items: " + ex.Message);
                    availableItems = [];
                }
            }
        
            private async Task LoadDocumentsAsync()
            {
                if (isLoadingDocuments) return;
        
                isLoadingDocuments = true;
                try
                {
                    var endDate = filterTo.Date.AddDays(1).AddTicks(-1);
                    var records = await InventoryService.GetStockGINRecordsAsync(
                        ResolveBranchId(),
                        filterFrom.Date,
                        endDate,
                        1,
                        200) ?? [];
        
                    documents = records
                        .Where(record => record != null)
                        .Select(record => new GinDocumentSummary
                        {
                            DocumentId = record.DocumentID ?? string.Empty,
                            DocumentNumber = string.IsNullOrWhiteSpace(record.DisplayCode)
                                ? record.DocumentID ?? string.Empty
                                : record.DisplayCode,
                            BranchId = record.BranchID ?? ResolveBranchId(),
                            FinancialDate = record.FinancialDate,
                            Activity = string.IsNullOrWhiteSpace(record.StockActivityType)
                                ? "Stock Issue"
                                : record.StockActivityType,
                            ReferenceNumber = record.ReferenceNumber ?? string.Empty,
                            TotalValue = record.TotalAfterTax,
                            Status = record.IsVoid ? "Void" : "Posted"
                        })
                        .Where(record => !string.IsNullOrWhiteSpace(record.DocumentId))
                        .OrderByDescending(record => record.FinancialDate)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[GoodsIssueNote] Unable to load GIN records: " + ex.Message);
                    ShowError("Unable to load goods issue notes from the server.");
                }
                finally
                {
                    isLoadingDocuments = false;
                }
            }
        
            private GinDraft NewDraft(string branchId) => new()
            {
                BranchId = branchId,
                Activity = "Internal Use",
                FinancialDate = DateTime.Today,
                PostingDate = DateTime.Today,
                Status = GinStatus.Draft
            };
        
            private void OpenNewDocument()
            {
                draft = NewDraft(ResolveBranchId());
                loadedGin = null;
                deletedServerLines.Clear();
                isEditingExisting = false;
                isEditorOpen = true;
                activityMenuOpen = false;
                DismissMessage();
            }
        
            private async Task OpenDocument(GinDocumentSummary document)
            {
                DismissMessage();
                isSaving = true;
        
                try
                {
                    var record = await InventoryService.GetStockGINRecordAsync(document.DocumentId);
                    if (record?.mobjDoc_Stock_GIN == null)
                    {
                        ShowError("Unable to load this GIN record.");
                        return;
                    }
        
                    loadedGin = record;
                    deletedServerLines.Clear();
                    ApplyRecordToDraft(record);
                    isEditingExisting = true;
                    isEditorOpen = true;
                    activityMenuOpen = false;
                    UpdateSummaryFromDraft();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[GoodsIssueNote] LoadRecord failed: " + ex.Message);
                    ShowError("Unable to load this GIN record.");
                }
                finally
                {
                    isSaving = false;
                }
            }
        
            private void ApplyRecordToDraft(GinDocumentDto record)
            {
                var header = record.mobjDoc_Stock_GIN;
        
                draft = new GinDraft
                {
                    DocumentId = header.DocumentID ?? string.Empty,
                    DocumentNumber = string.IsNullOrWhiteSpace(header.DisplayCode)
                        ? header.DocumentID ?? string.Empty
                        : header.DisplayCode,
                    BranchId = string.IsNullOrWhiteSpace(header.BranchID) ? ResolveBranchId() : header.BranchID,
                    Activity = string.IsNullOrWhiteSpace(header.StockActivityType) ? "Internal Use" : header.StockActivityType,
                    FinancialDate = header.FinancialDate == default ? DateTime.Today : header.FinancialDate,
                    PostingDate = header.PostingDate == default ? header.FinancialDate : header.PostingDate,
                    ReferenceNumber = header.ReferenceNumber ?? string.Empty,
                    AccountName = header.AccountName ?? string.Empty,
                    Remarks = header.Remarks ?? string.Empty,
                    Status = header.IsVoid ? GinStatus.Void : GinStatus.Posted,
                    Lines = (record.lstDocumentLine ?? [])
                        .Where(line => line.SaveAction != EBI.Enum.EntityState.Deleted)
                        .Select(ToDraftLine)
                        .ToList()
                };
            }
        
            private GinLine ToDraftLine(DocumentLineTableDM line)
            {
                var itemId = line.InventoryItemAccountID ?? line.LineItemID ?? string.Empty;
                var inventory = availableItems.FirstOrDefault(item =>
                    string.Equals(item.MasterAccountID, itemId, StringComparison.OrdinalIgnoreCase));
        
                var cost = line.Cost != 0 ? line.Cost : line.UnitPrice;
        
                return new GinLine
                {
                    Source = line,
                    ItemId = itemId,
                    ItemCode = !string.IsNullOrWhiteSpace(line.LineItemDisplayCode)
                        ? line.LineItemDisplayCode
                        : inventory?.ProductCode ?? line.LineItemID ?? itemId,
                    ItemName = !string.IsNullOrWhiteSpace(line.Description)
                        ? line.Description
                        : !string.IsNullOrWhiteSpace(line.ItemName)
                            ? line.ItemName
                            : inventory?.AccountName ?? "Unnamed item",
                    UomId = line.UnitOfMeasurementID ?? inventory?.UnitOfMeasureID ?? string.Empty,
                    Uom = inventory?.UnitOfMeasureName ?? line.UnitOfMeasurementID ?? "UNIT",
                    InventoryTypeId = line.InventoryTypeID,
                    SkuQuantity = line.SKUQuantity,
                    Quantity = line.Quantity,
                    Cost = cost
                };
            }
        
            private void CloseEditor()
            {
                isEditorOpen = false;
                showItemPicker = false;
                activityMenuOpen = false;
                loadedGin = null;
                deletedServerLines.Clear();
                DismissMessage();
            }
        
            private void HandleBack()
            {
                if (showItemPicker)
                {
                    CloseItemPicker();
                    return;
                }
        
                if (isEditorOpen)
                {
                    CloseEditor();
                    return;
                }
        
                Navigation.NavigateTo("/home");
            }
        
            private async Task ToggleNav() => await OnToggleNav.InvokeAsync();
        
            private void ToggleActivityMenu() => activityMenuOpen = !activityMenuOpen;
        
            private void CloseActivityMenu() => activityMenuOpen = false;
        
            private void SelectActivity(string activity)
            {
                draft.Activity = activity;
                activityMenuOpen = false;
            }
        
            private void OpenItemPicker()
            {
                activityMenuOpen = false;
                showItemPicker = true;
            }
        
            private void CloseItemPicker() => showItemPicker = false;
        
            private async Task HandleProductSelected(string masterAccountId)
            {
                var item = availableItems.FirstOrDefault(candidate => candidate.MasterAccountID == masterAccountId);
                if (item == null)
                {
                    await LoadItemsAsync();
                    item = availableItems.FirstOrDefault(candidate => candidate.MasterAccountID == masterAccountId);
                }
        
                if (item == null)
                {
                    CloseItemPicker();
                    ShowError("Unable to load the selected product details.");
                    return;
                }
        
                AddItem(item);
            }
        
            private void AddItem(InventoryDM item)
            {
                var itemId = item.MasterAccountID ?? string.Empty;
                var existing = draft.Lines.FirstOrDefault(line => line.ItemId == itemId);
                if (existing != null)
                {
                    existing.Quantity += 1;
                }
                else
                {
                    draft.Lines.Add(new GinLine
                    {
                        ItemId = itemId,
                        ItemCode = string.IsNullOrWhiteSpace(item.ProductCode) ? itemId : item.ProductCode,
                        ItemName = item.AccountName ?? "Unnamed item",
                        UomId = item.UnitOfMeasureID ?? string.Empty,
                        Uom = item.UnitOfMeasureName ?? item.UnitOfMeasureID ?? "UNIT",
                        InventoryTypeId = item.InventoryTypeID,
                        SkuQuantity = item.UOMBase == 0 ? 1 : item.UOMBase,
                        Quantity = 1,
                        Cost = item.PurchasePrice
                    });
                }
        
                CloseItemPicker();
            }
        
            private void RemoveLine(GinLine line)
            {
                if (line.Source != null && !string.IsNullOrWhiteSpace(line.Source.DocumentLineID))
                {
                    line.Source.SaveAction = EBI.Enum.EntityState.Deleted;
                    line.Source.IsDirty = true;
                    deletedServerLines.Add(line.Source);
                }
        
                draft.Lines.Remove(line);
            }
        
            private void ChangeQuantity(GinLine line, int delta)
            {
                line.Quantity = Math.Max(0.01m, line.Quantity + delta);
            }
        
            private async Task SaveDraft()
            {
                await PersistGINAsync(closeAfterSave: false);
            }
        
            private async Task PostDocument()
            {
                await PersistGINAsync(closeAfterSave: true);
            }
        
            private async Task PersistGINAsync(bool closeAfterSave)
            {
                if (!ValidateDraft() || isSaving)
                    return;
        
                isSaving = true;
                DismissMessage();
        
                try
                {
                    var request = BuildGINRequest();
        
                    if (isEditingExisting)
                    {
                        var updateResult = await InventoryService.UpdateStockGINAsync(request);
                        if (!updateResult.Success)
                        {
                            ShowError(updateResult.Message);
                            return;
                        }
        
                        draft.Status = GinStatus.Posted;
                        deletedServerLines.Clear();
                        messageType = "success";
                        messageText = updateResult.Message;
                    }
                    else
                    {
                        var createResult = await InventoryService.CreateStockGINAsync(request);
                        if (!createResult.Success)
                        {
                            ShowError(createResult.Message);
                            return;
                        }
        
                        draft.Status = GinStatus.Posted;
                        draft.DocumentId = createResult.DocumentId ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(draft.DocumentId))
                            request.mobjDoc_Stock_GIN.DocumentID = draft.DocumentId;
                        loadedGin = request;
                        isEditingExisting = true;
                        messageType = "success";
                        messageText = createResult.Message;
                    }
        
                    if (!string.IsNullOrWhiteSpace(draft.DocumentId))
                    {
                        var saved = await InventoryService.GetStockGINRecordAsync(draft.DocumentId);
                        if (saved?.mobjDoc_Stock_GIN != null)
                        {
                            loadedGin = saved;
                            ApplyRecordToDraft(saved);
                        }
                    }
        
                    await LoadDocumentsAsync();
                    UpdateSummaryFromDraft();
        
                    if (closeAfterSave)
                    {
                        isEditorOpen = false;
                        showItemPicker = false;
                        activityMenuOpen = false;
                        loadedGin = null;
                        deletedServerLines.Clear();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[GoodsIssueNote] Save failed: " + ex.Message);
                    ShowError("Failed to save the goods issue note.");
                }
                finally
                {
                    isSaving = false;
                }
            }
        
            private GinDocumentDto BuildGINRequest()
            {
                var request = isEditingExisting && loadedGin != null
                    ? loadedGin
                    : new GinDocumentDto();
        
                var header = request.mobjDoc_Stock_GIN;
                header.DocumentTypeID = (int)EBI.Enum.EnumDocumentType.GIN;
                header.BranchID = draft.BranchId;
                header.EditBranchID = draft.BranchId;
                header.FinancialDate = draft.FinancialDate;
                header.PostingDate = draft.PostingDate;
                header.IsPostingDateDifferent = draft.PostingDate.Date != draft.FinancialDate.Date;
                header.ReferenceNumber = draft.ReferenceNumber;
                header.AccountName = draft.AccountName;
                header.Remarks = draft.Remarks;
                header.StockActivityType = draft.Activity;
                header.GroupID = AppState.SelectedBranchGroupID;
                header.TotalBeforeTax = draft.TotalValue;
                header.TotalAfterTax = draft.TotalValue;
                header.SaveAction = isEditingExisting ? EBI.Enum.EntityState.Changed : EBI.Enum.EntityState.Added;
                header.IsDirty = true;
        
                request.lstDocumentLine.Clear();
        
                var lineOrder = 0;
                foreach (var line in draft.Lines)
                {
                    var row = line.Source ?? new DocumentLineTableDM();
        
                    row.LineOrder = lineOrder++;
                    row.OwnerDocumentTypeID = (int)EBI.Enum.EnumDocumentType.GIN;
                    row.Description = line.ItemName;
                    row.ItemName = line.ItemName;
                    row.InventoryTypeID = line.InventoryTypeId;
                    row.InventoryItemAccountID = line.ItemId;
                    row.LineItemID = line.ItemId;
                    row.Quantity = line.Quantity;
                    row.UnitPrice = line.Cost;
                    row.Cost = line.Cost;
                    row.SubTotal = line.Total;
                    row.SubTotalBeforeGST = line.Total;
                    row.UnitOfMeasurementID = line.UomId;
                    row.SKUQuantity = line.SkuQuantity == 0 ? 1 : line.SkuQuantity;
                    row.BranchID = draft.BranchId;
                    row.EditBranchID = draft.BranchId;
                    row.GroupID = AppState.SelectedBranchGroupID;
                    row.FinancialDate = draft.FinancialDate;
                    row.SaveAction = line.Source == null ? EBI.Enum.EntityState.Added : EBI.Enum.EntityState.Changed;
                    row.IsDirty = true;
        
                    request.lstDocumentLine.Add(row);
                }
        
                foreach (var deletedLine in deletedServerLines)
                {
                    deletedLine.SaveAction = EBI.Enum.EntityState.Deleted;
                    deletedLine.IsDirty = true;
                    request.lstDocumentLine.Add(deletedLine);
                }
        
                return request;
            }
        
            private bool ValidateDraft()
            {
                if (string.IsNullOrWhiteSpace(draft.BranchId))
                {
                    ShowError("Select a branch before saving.");
                    return false;
                }
        
                if (!draft.Lines.Any())
                {
                    ShowError("Add at least one item.");
                    return false;
                }
        
                if (draft.Lines.Any(line => line.Quantity <= 0))
                {
                    ShowError("Issue quantity must be greater than zero.");
                    return false;
                }
        
                return true;
            }
        
            private void UpdateSummaryFromDraft()
            {
                if (string.IsNullOrWhiteSpace(draft.DocumentId))
                    return;
        
                var summary = documents.FirstOrDefault(item =>
                    string.Equals(item.DocumentId, draft.DocumentId, StringComparison.OrdinalIgnoreCase));
        
                if (summary == null)
                    return;
        
                if (!string.IsNullOrWhiteSpace(draft.DocumentNumber))
                    summary.DocumentNumber = draft.DocumentNumber;
        
                summary.BranchId = draft.BranchId;
                summary.FinancialDate = draft.FinancialDate;
                summary.Activity = draft.Activity;
                summary.ReferenceNumber = draft.ReferenceNumber;
                summary.ItemCount = draft.Lines.Count;
                summary.TotalQuantity = draft.Lines.Sum(line => line.Quantity);
                summary.TotalValue = draft.TotalValue;
                summary.Status = draft.Status == GinStatus.Void ? "Void" : "Posted";
            }
        
            private void ShowError(string text)
            {
                messageType = "error";
                messageText = text;
            }
        
            private void DismissMessage()
            {
                messageText = string.Empty;
                messageType = "success";
            }
        
            private void ClearSearch() => searchText = string.Empty;
        
            private async Task RefreshList()
            {
                await LoadDocumentsAsync();
                StateHasChanged();
            }
        
            private sealed class GinDraft
            {
                public string DocumentId { get; set; } = string.Empty;
                public string DocumentNumber { get; set; } = string.Empty;
                public string BranchId { get; set; } = string.Empty;
                public DateTime FinancialDate { get; set; }
                public DateTime PostingDate { get; set; }
                public string Activity { get; set; } = "Internal Use";
                public string ReferenceNumber { get; set; } = string.Empty;
                public string AccountName { get; set; } = string.Empty;
                public string Remarks { get; set; } = string.Empty;
                public GinStatus Status { get; set; }
                public List<GinLine> Lines { get; set; } = [];
                public decimal TotalValue => Lines.Sum(line => line.Total);
            }
        
            private sealed class GinLine
            {
                public DocumentLineTableDM? Source { get; set; }
                public string ItemId { get; set; } = string.Empty;
                public string ItemCode { get; set; } = string.Empty;
                public string ItemName { get; set; } = string.Empty;
                public string UomId { get; set; } = string.Empty;
                public string Uom { get; set; } = "UNIT";
                public int InventoryTypeId { get; set; }
                public decimal SkuQuantity { get; set; }
                public decimal Quantity { get; set; } = 1;
                public decimal Cost { get; set; }
                public decimal Total => Quantity * Cost;
            }
        
            private sealed class GinDocumentSummary
            {
                public string DocumentId { get; set; } = string.Empty;
                public string DocumentNumber { get; set; } = string.Empty;
                public string BranchId { get; set; } = string.Empty;
                public DateTime FinancialDate { get; set; }
                public string Activity { get; set; } = string.Empty;
                public string ReferenceNumber { get; set; } = string.Empty;
                public int ItemCount { get; set; }
                public decimal TotalQuantity { get; set; }
                public decimal TotalValue { get; set; }
                public string Status { get; set; } = "Posted";
            }
        
            private enum GinStatus
            {
                Draft,
                Posted,
                Void
            }
    }
}
