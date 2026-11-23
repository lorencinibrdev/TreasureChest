namespace TreasureChest.Source.Services.Install;

public class InstalledGameManifest
{
    // Properties:
    public required long GameId { get; init; }
    public required string GameSlug { get; init; }
    public required string InstallDirectoryPath { get; init; }
    public required GogPlayTaskEntry PrimaryTask { get; init; }
    public List<GogPlayTaskEntry> OtherTasks { get; init; } = [];
    public DateTime InstallDate { get; init; } = DateTime.Now;
}