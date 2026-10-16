using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class OwnedGamesResponse
{
    // Properties:
    [JsonPropertyName("owned")] public List<long>? Games { get; init; }
}