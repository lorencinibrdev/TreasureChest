using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class GogUserDataResponse
{
    // Methods:
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(UserId) &&  
               !string.IsNullOrEmpty(Username) && 
               !string.IsNullOrEmpty(Email);
    }
    
    public static GogUserDataResponse Empty => new()
    {
        UserId = "DUMMY_USER_ID",
        Username = "DUMMY_USER_NAME",
        Email = "DUMMY_EMAIL",
    };
    
    // Properties:
    [JsonPropertyName("userId")] public string? UserId { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}