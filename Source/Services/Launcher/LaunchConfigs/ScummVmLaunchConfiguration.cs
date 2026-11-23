namespace TreasureChest.Source.Services.Launcher.LaunchConfigs;

public record ScummVmLaunchConfiguration : LaunchConfiguration
{
    // Properties:
    public string GameTargetId { get; init; } = string.Empty;
    public string SaveDirectoryPath { get; init; } = string.Empty;
    public bool ForceFullscreen { get; init; } = true;
}