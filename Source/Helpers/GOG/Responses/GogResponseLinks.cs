// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseLinks
{
    // Properties:
    [JsonPropertyName("product_card")] public string? Product { get; init; }
    [JsonPropertyName("support")] public string? Support { get; init; }
    [JsonPropertyName("forum")] public string? Forum { get; init; }
}