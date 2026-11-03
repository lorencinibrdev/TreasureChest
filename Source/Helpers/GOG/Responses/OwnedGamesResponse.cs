// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogOwnedGamesListResponse
{
    // Properties:
    [JsonPropertyName("owned")] public List<long>? Games { get; init; }
}