using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class ProductDescriptionResponse
{
    // Properties:
    [JsonPropertyName("description")] public DescriptionData? Description { get; init; }
}

public class DescriptionData
{
    // Properties:
    [JsonPropertyName("lead")] public string? Lead { get; init; }
    [JsonPropertyName("full")] public string? Full { get; init; }
}