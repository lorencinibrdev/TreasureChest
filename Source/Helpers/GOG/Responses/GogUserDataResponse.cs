// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogUserDataResponse
{
    // Properties:
    [JsonPropertyName("userId")] public string? UserId { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    public bool IsValid => !string.IsNullOrWhiteSpace(UserId) &&  
                           !string.IsNullOrWhiteSpace(Username) && 
                           !string.IsNullOrWhiteSpace(Email);
}