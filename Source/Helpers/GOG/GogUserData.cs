using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class GogUserData
{
    [JsonPropertyName("userId")] public string? UserId { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}