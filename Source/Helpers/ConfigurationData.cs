namespace TreasureChest.Source.Helpers;

public class ConfigurationData
{
    // Properties:
    public bool IsFirstRun { get; set; } = true;
    public bool IsLoggedIn { get; set; } = false;
    public string SavedUserName { get; set; } = "";
    public string SavedUserId { get; set; } = "";
    public string SavedUserEmail { get; set; } = "";
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
}