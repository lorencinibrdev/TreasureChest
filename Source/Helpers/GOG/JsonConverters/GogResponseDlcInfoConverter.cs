using System.Text.Json;
using System.Text.Json.Serialization;
using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Helpers.GOG.JsonConverters;

public class GogResponseDlcInfoConverter : JsonConverter<GogResponseDlcInformation>
{
    public override GogResponseDlcInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        if (document.RootElement.ValueKind is not JsonValueKind.Object)
            return new GogResponseDlcInformation { HasDlc = false };

        string? url = null;
        string? expandedUrl = null;

        if (document.RootElement.TryGetProperty("all_products_url", out var urlElement))
        {
            url = urlElement.GetString();                
        }
            
        if (document.RootElement.TryGetProperty("expanded_all_products_url", out var expandedUrlElement))
        {
            expandedUrl = expandedUrlElement.GetString();                
        }

        return new GogResponseDlcInformation
        {
            HasDlc = true,
            AllProductsUrl = url,
            ExpandedAllProductsUrl = expandedUrl
        };

    }
    
    public override void Write(Utf8JsonWriter writer, GogResponseDlcInformation value, JsonSerializerOptions options)
    {
        if (!value.HasDlc)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
            return;
        }
        
        writer.WriteStartObject();

        if (!string.IsNullOrEmpty(value.AllProductsUrl))
        {
            writer.WriteString("all_products_url", value.AllProductsUrl);
        }

        if (!string.IsNullOrEmpty(value.ExpandedAllProductsUrl))
        {
            writer.WriteString("expanded_all_products_url", value.ExpandedAllProductsUrl);
        }
        
        writer.WriteEndObject();
    }
}