// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseDescriptionData
{
    // Properties:
    [JsonPropertyName("lead")] public string? Lead { get; init; }
    [JsonPropertyName("full")] public string? Full { get; init; }
}