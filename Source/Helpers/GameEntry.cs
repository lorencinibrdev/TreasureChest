using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Helpers;

public class GameEntry
{
    // Private Members:
    
    
    
    // Properties:
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required SystemCompatibility SystemCompatibility { get; init; }
    public required Links Links { get; init; }
    public required bool IsInstallable { get; init; }
}




/*

// Properties:
   [JsonPropertyName("id")] public long? Id { get; init; }
   [JsonPropertyName("title")] public string? Title { get; init; }
   [JsonPropertyName("slug")] public string? Slug { get; init; }
   [JsonPropertyName("content_system_compatibility")] public SystemCompatibility? SystemCompatibility { get; init; }
   [JsonPropertyName("links")] public Links? Links { get; init; }
   [JsonPropertyName("is_installable")] public bool? IsInstallable { get; init; }
   [JsonPropertyName("game_type")] public string? GameType { get; init; }
   [JsonPropertyName("images")] public ImageLinks? Images { get; init; }
   [JsonPropertyName("dlcs")] [JsonConverter(typeof(GogDlcInfoConverter))] public DlcInformation? DlcInformation { get; init; }


*/