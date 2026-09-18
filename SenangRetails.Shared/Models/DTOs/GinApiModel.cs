using EBI.DM;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs;

/// <summary>
/// Wrapper matching the Doc_Stock_GIN API contract.
/// Uses the EBI domain models directly, the same way the GRN integration does.
/// </summary>
public sealed class GinDocumentDto
{
    [JsonPropertyName("mobjDoc_Stock_GIN")]
    public Doc_Stock_GINDM mobjDoc_Stock_GIN { get; set; } = new();

    [JsonPropertyName("lstDocumentLine")]
    public List<DocumentLineTableDM> lstDocumentLine { get; set; } = new();
}
