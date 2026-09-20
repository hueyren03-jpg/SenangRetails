using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{

    [JsonConverter(typeof(StockTransferApiResultConverter))]
    public sealed class StockTransferApiResult
    {
        public string? Id { get; set; }
        public string? DisplayCode { get; set; }
        public string? SuccessMessage { get; set; }
    }

    public sealed class StockTransferApiResultConverter : JsonConverter<StockTransferApiResult>
    {
        public override StockTransferApiResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new StockTransferApiResult
                {
                    Id = reader.GetString()
                };
            }

            if (reader.TokenType == JsonTokenType.Null)
                return null;

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            return new StockTransferApiResult
            {
                Id = GetString(root, "Id"),
                DisplayCode = GetString(root, "DisplayCode"),
                SuccessMessage = GetString(root, "SuccessMessage")
            };
        }

        public override void Write(Utf8JsonWriter writer, StockTransferApiResult value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Id", value.Id);
            writer.WriteString("DisplayCode", value.DisplayCode);
            writer.WriteString("SuccessMessage", value.SuccessMessage);
            writer.WriteEndObject();
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString();
            }

            return null;
        }
    }

    public sealed class StockTransferSaveResult
    {
        public bool TransferSaved { get; init; }
        public bool Success => TransferSaved;
        public string Message { get; init; } = "";
        public string? TransferDocumentId { get; init; }
    }

    public class StockTransferModel
    {
        public string DocNo { get; set; } = "";
        public string Branch { get; set; } = "";
        public string FromBranch { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Today;
        public string Remarks { get; set; } = "";
        public List<StockTransferDetailModel> Items { get; set; } = new();

        // Helpers
        public int TotalQty => Items?.Sum(i => i.Qty) ?? 0;
        public int DistinctItemsCount => Items?.Count ?? 0;
    }

    public class StockTransferDetailModel
    {
        public string DocNo { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Matrix { get; set; } = "";
        public int Qty { get; set; } = 1;
    }
}
