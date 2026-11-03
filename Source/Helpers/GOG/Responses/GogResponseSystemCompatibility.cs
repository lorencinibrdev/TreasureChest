// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseSystemCompatibility
{
    // Properties:
    [JsonPropertyName("windows")] public bool? Windows { get; init; }
    [JsonPropertyName("osx")] public bool? MacOs { get; init; }
    [JsonPropertyName("linux")] public bool? Linux { get; init; }
}