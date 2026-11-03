using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseDownloadPlatforms
{
    [JsonPropertyName("windows")] public List<GogResponseDownloadItem>? Windows { get; init; }
    [JsonPropertyName("mac")] public List<GogResponseDownloadItem>? Mac { get; init; }
    [JsonPropertyName("linux")] public List<GogResponseDownloadItem>? Linux { get; init; }
    public bool HasData => Windows is not null || Mac is not null || Linux is not null;
}