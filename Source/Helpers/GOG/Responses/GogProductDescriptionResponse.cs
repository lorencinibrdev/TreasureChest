// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogProductDescriptionResponse
{
    // Properties:
    [JsonPropertyName("description")] public GogResponseDescriptionData? Description { get; init; }
}