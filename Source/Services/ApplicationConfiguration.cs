namespace TreasureChest.Source.Services;

public class ApplicationConfiguration
{
    // Properties:
    public required bool IsFirstRun { get; set; }
    public bool IsLoggedIn { get; set; }
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
}