using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class GogTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
}