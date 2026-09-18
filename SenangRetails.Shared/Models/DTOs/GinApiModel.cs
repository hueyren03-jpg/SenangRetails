using EBI.DM;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs;

/// <summary>
/// Wrapper matching the Doc_Stock_GIN API contract.
/// Uses the available EBI stock-document header model. The current EBI.DM assembly does not expose Doc_Stock_GINDM.
/// </summary>
public sealed class GinDocumentDto
{
    [JsonPropertyName("mobjDoc_Stock_GIN")]
    public Doc_Stock_GRNDM mobjDoc_Stock_GIN { get; set; } = new();

    [JsonPropertyName("lstDocumentLine")]
    public List<DocumentLineTableDM> lstDocumentLine { get; set; } = new();
}
