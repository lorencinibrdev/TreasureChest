// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseExtraContent
{
    [JsonPropertyName("manualUrl")] public string? ManualUrl { get; init; }
    [JsonPropertyName("downloaderUrl")] public string? DownloaderUrl { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("info")] public int Info { get; init; }
    [JsonPropertyName("size")] public string? Size { get; init; }
}