using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs;

[JsonConverter(typeof(StockDocumentApiResultConverter))]
public sealed class StockDocumentApiResult
{
    public string? Id { get; set; }
    public string? DisplayCode { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class StockDocumentApiResultConverter : JsonConverter<StockDocumentApiResult>
{
    public override StockDocumentApiResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new StockDocumentApiResult { Id = reader.GetString() };
        }

        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new StockDocumentApiResult
        {
            Id = GetString(root, "Id"),
            DisplayCode = GetString(root, "DisplayCode"),
            SuccessMessage = GetString(root, "SuccessMessage")
        };
    }

    public override void Write(Utf8JsonWriter writer, StockDocumentApiResult value, JsonSerializerOptions options)
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
