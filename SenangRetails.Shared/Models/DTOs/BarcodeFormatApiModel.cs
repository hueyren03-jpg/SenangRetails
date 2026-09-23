namespace SenangRetails.Shared.Models.DTOs;

public sealed class BarcodeFormatApiModel
{
    public bool IsLoading { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ItemCodeLength { get; set; }
    public int QuantityLength { get; set; }
    public string QuantityFixedAsOne { get; set; } = "NO";
    public int PriceLength { get; set; }
    public int PriceDecimalPlace { get; set; }
    public int CheckSumLength { get; set; }

    // Backend fields are preserved internally for API round-trips.
    // They are intentionally not exposed on the current Barcode Reading Setup UI.
    public int SectionInventoryID { get; set; }
    public int SectionBarcode { get; set; }
    public int SectionDescription { get; set; }
    public int SectionBatch { get; set; }
    public int SectionExpiryDate { get; set; }
    public int SectionSerialNo { get; set; }
    public int SectionUOM { get; set; }
    public int SectionMatrix { get; set; }

    public string BarcodeType { get; set; } = "BarCode";
    public bool HasOverlappingBarcodeItems { get; set; }
    public bool IsBarcodeIncludesPrefix { get; set; }
    public EBI.Enum.EntityState SaveAction { get; set; }
    public bool IsDirty { get; set; }
}
