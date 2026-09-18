using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs;

/// <summary>
/// API contract for /api/Doc_Stock_GIN.
/// The bundled EBI client DLLs do not contain Doc_Stock_GIN, so this DTO mirrors
/// the Swagger contract while preserving any server fields we do not explicitly map.
/// </summary>
public sealed class GinDocumentDto
{
    [JsonPropertyName("mobjDoc_Stock_GIN")]
    public GinHeaderDto mobjDoc_Stock_GIN { get; set; } = new();

    [JsonPropertyName("lstDocumentLine")]
    public List<GinLineDto> lstDocumentLine { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class GinHeaderDto
{
    [JsonPropertyName("documentID")]
    public string? DocumentID { get; set; }

    [JsonPropertyName("documentTypeID")]
    public int DocumentTypeID { get; set; }

    [JsonPropertyName("friendlyDocumentName")]
    public string? FriendlyDocumentName { get; set; }

    [JsonPropertyName("alphaCode")]
    public string? AlphaCode { get; set; }

    [JsonPropertyName("numericCode")]
    public int NumericCode { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("editBranchID")]
    public string? EditBranchID { get; set; }

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("financialDate")]
    public DateTime FinancialDate { get; set; }

    [JsonPropertyName("accountID")]
    public string? AccountID { get; set; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("referenceNumber")]
    public string? ReferenceNumber { get; set; }

    [JsonPropertyName("totalBeforeTax")]
    public decimal TotalBeforeTax { get; set; }

    [JsonPropertyName("taxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("roundingAmount")]
    public decimal RoundingAmount { get; set; }

    [JsonPropertyName("totalAfterTax")]
    public decimal TotalAfterTax { get; set; }

    [JsonPropertyName("transactionCurrencyID")]
    public string? TransactionCurrencyID { get; set; }

    [JsonPropertyName("localCurrencyID")]
    public string? LocalCurrencyID { get; set; }

    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("createdByDocumentTypeID")]
    public int CreatedByDocumentTypeID { get; set; }

    [JsonPropertyName("createdByDocumentTypeName")]
    public string? CreatedByDocumentTypeName { get; set; }

    [JsonPropertyName("createdByDocumentID")]
    public string? CreatedByDocumentID { get; set; }

    [JsonPropertyName("createdByDocumentDisplayCode")]
    public string? CreatedByDocumentDisplayCode { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("isVoid")]
    public bool IsVoid { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("transactionCurrencyName")]
    public string? TransactionCurrencyName { get; set; }

    [JsonPropertyName("localCurrencyName")]
    public string? LocalCurrencyName { get; set; }

    [JsonPropertyName("groupID")]
    public string? GroupID { get; set; }

    [JsonPropertyName("financialAccountID")]
    public string? FinancialAccountID { get; set; }

    [JsonPropertyName("postingDate")]
    public DateTime PostingDate { get; set; }

    [JsonPropertyName("isPostingDateDifferent")]
    public bool IsPostingDateDifferent { get; set; }

    [JsonPropertyName("stockActivityType")]
    public string? StockActivityType { get; set; }

    [JsonPropertyName("saveAction")]
    public string SaveAction { get; set; } = "Added";

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class GinLineDto
{
    [JsonPropertyName("documentLineID")]
    public string? DocumentLineID { get; set; }

    [JsonPropertyName("documentID")]
    public string? DocumentID { get; set; }

    [JsonPropertyName("documentLineTypeID")]
    public int DocumentLineTypeID { get; set; }

    [JsonPropertyName("ownerDocumentTypeID")]
    public int OwnerDocumentTypeID { get; set; }

    [JsonPropertyName("lineOrder")]
    public int LineOrder { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("subTotal")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("inventoryItemAccountID")]
    public string? InventoryItemAccountID { get; set; }

    [JsonPropertyName("adjustedQuantity")]
    public decimal AdjustedQuantity { get; set; }

    [JsonPropertyName("adjustedValue")]
    public decimal AdjustedValue { get; set; }

    [JsonPropertyName("inventoryTypeID")]
    public int InventoryTypeID { get; set; }

    [JsonPropertyName("lineItemID")]
    public string? LineItemID { get; set; }

    [JsonPropertyName("unitOfMeasurementID")]
    public string? UnitOfMeasurementID { get; set; }

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }

    [JsonPropertyName("itemName")]
    public string? ItemName { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("activityTypeID")]
    public int ActivityTypeID { get; set; }

    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("currencyID")]
    public string? CurrencyID { get; set; }

    [JsonPropertyName("sourceDocumentLineID")]
    public string? SourceDocumentLineID { get; set; }

    [JsonPropertyName("gstTypeID")]
    public string? GSTTypeID { get; set; }

    [JsonPropertyName("taxPercentage")]
    public decimal TaxPercentage { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("subTotalBeforeGST")]
    public decimal SubTotalBeforeGST { get; set; }

    [JsonPropertyName("isTaxInclusive")]
    public bool IsTaxInclusive { get; set; }

    [JsonPropertyName("skuName")]
    public string? SKUName { get; set; }

    [JsonPropertyName("skuQuantity")]
    public decimal SKUQuantity { get; set; }

    [JsonPropertyName("taxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("lineItemDisplayCode")]
    public string? LineItemDisplayCode { get; set; }

    [JsonPropertyName("editBranchID")]
    public string? EditBranchID { get; set; }

    [JsonPropertyName("groupID")]
    public string? GroupID { get; set; }

    [JsonPropertyName("financialDate")]
    public DateTime FinancialDate { get; set; }

    [JsonPropertyName("saveAction")]
    public string SaveAction { get; set; } = "Added";

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
