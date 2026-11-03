// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseDownloadItem
{
    // Properties:
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("date")] public string? Date { get; init; }
    [JsonPropertyName("size")] public string? Size { get; init; }
    [JsonPropertyName("manualUrl")] public string? ManualUrl { get; init; }
}