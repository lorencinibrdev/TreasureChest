using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class SystemCompatibility
{
    // Properties:
    [JsonPropertyName("windows")] public bool? Windows { get; init; }
    [JsonPropertyName("osx")] public bool? MacOs { get; init; }
    [JsonPropertyName("linux")] public bool? Linux { get; init; }
}

public class Links
{
    // Properties:
    [JsonPropertyName("purchase_link")] public string? PurchaseLink { get; init; }
    [JsonPropertyName("product_card")] public string? Product { get; init; }
    [JsonPropertyName("support")] public string? Support { get; init; }
    [JsonPropertyName("forum")] public string? Forum { get; init; }
}

public class ImageLinks
{
    // Properties:
    [JsonPropertyName("background")] public string? Background { get; init; }
    [JsonPropertyName("logo")] public string? Logo { get; init; }
    [JsonPropertyName("logo2x")] public string? Logo2X { get; init; }
    [JsonPropertyName("sidebarIcon")] public string? SidebarIcon { get; init; }
    [JsonPropertyName("sidebarIcon2x")] public string? SideBarIcon2X { get; init; }
    [JsonPropertyName("menuNotificationAv")] public string? MenuNotificationAv { get; init; }
    [JsonPropertyName("menuNotificationAv2")] public string? MenuNotificationAv2 { get; init; }
    [JsonPropertyName("icon")] public string? Icon { get; init; }
}

public class DlcInformation
{
    // Properties:
    public bool HasDlc { get; init; }
    public string? AllProductsUrl { get; init; }
    public string? ExpandedAllProductsUrl { get; init; }
}

public class GameDetailsResponse
{
    // Properties:
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("content_system_compatibility")] public SystemCompatibility? SystemCompatibility { get; init; }
    [JsonPropertyName("links")] public Links? Links { get; init; }
    [JsonPropertyName("is_installable")] public bool? IsInstallable { get; init; }
    [JsonPropertyName("game_type")] public string? GameType { get; init; }
    [JsonPropertyName("images")] public ImageLinks? Images { get; init; }
    [JsonPropertyName("dlcs")] [JsonConverter(typeof(GogDlcInfoConverter))] public DlcInformation? DlcInformation { get; init; }
}

public class GogDlcInfoConverter : JsonConverter<DlcInformation>
{
    public override DlcInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        if (document.RootElement.ValueKind is JsonValueKind.Array or not JsonValueKind.Object)
            return new DlcInformation { HasDlc = false };

        string? url = null;
        string? expandedUrl = null;

        if (document.RootElement.TryGetProperty("all_products_url", out var urlElement))
        {
            url = urlElement.GetString();                
        }
            
        if (document.RootElement.TryGetProperty("expanded_all_products_url", out var expandedUrlElement))
        {
            expandedUrl = urlElement.GetString();                
        }

        return new DlcInformation
        {
            HasDlc = true,
            AllProductsUrl = url,
            ExpandedAllProductsUrl = expandedUrl
        };

    }
    
    public override void Write(Utf8JsonWriter writer, DlcInformation value, JsonSerializerOptions options)
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