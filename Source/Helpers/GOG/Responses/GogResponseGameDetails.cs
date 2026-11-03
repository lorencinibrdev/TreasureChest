// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;
using TreasureChest.Source.Helpers.GOG.JsonConverters;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseGameDetails
{
    // Properties:
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("content_system_compatibility")] public GogResponseSystemCompatibility? SystemCompatibility { get; init; }
    [JsonPropertyName("links")] public GogResponseLinks? Links { get; init; }
    [JsonPropertyName("is_installable")] public bool? IsInstallable { get; init; }
    [JsonPropertyName("game_type")] public string? GameType { get; init; }
    [JsonPropertyName("images")] public GogResponseImageLinks? Images { get; init; }
    [JsonPropertyName("dlcs")] [JsonConverter(typeof(GogResponseDlcInfoConverter))] public GogResponseDlcInformation? DlcInformation { get; init; }
}