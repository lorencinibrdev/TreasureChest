// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global

using System.Text.Json.Serialization;
using TreasureChest.Source.Helpers.GOG.JsonConverters;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseGameContent
{
    // Properties:
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("cdKey")] public string? CdKey { get; init; }
    [JsonPropertyName("textInformation")] public string? TextInformation { get; init; }
    [JsonPropertyName("downloads")] [JsonConverter(typeof(GogResponseDownloadsListConverter))] public List<GogResponseDownloadLanguage>? Downloads { get; init; }
    [JsonPropertyName("extras")] public List<GogResponseExtraContent>? Extras { get; init; }
    [JsonPropertyName("dlcs")] public List<GogResponseGameContent>? DlcInformation { get; init; }
    [JsonPropertyName("tags")] public List<object>? Tags { get; init; }
    [JsonPropertyName("isPreOrder")] public bool IsPreOrder { get; init; }
    [JsonPropertyName("releaseTimestamp")] public long ReleaseTimestamp { get; init; }
    [JsonPropertyName("messages")] public List<object>? Messages { get; init; } 
    [JsonPropertyName("changelog")] public string? Changelog { get; init; } 
    [JsonPropertyName("forumLink")] public string? ForumLink { get; init; }
    [JsonPropertyName("isBaseProductMissing")] public bool IsBaseProductMissing { get; init; }
    [JsonPropertyName("missingBaseProduct")] public object? MissingBaseProduct { get; init; }
}