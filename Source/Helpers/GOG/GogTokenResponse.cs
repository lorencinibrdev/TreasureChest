using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class GogTokenResponse
{
    // Methods:
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(AccessToken) &&  
               !string.IsNullOrEmpty(RefreshToken);
    }
    
    // Properties:
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
}