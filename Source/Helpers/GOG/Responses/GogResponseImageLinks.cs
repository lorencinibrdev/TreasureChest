// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseImageLinks
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