namespace TreasureChest.Source.Services.Launcher.LaunchConfigs;

public record DosBoxLaunchConfiguration : LaunchConfiguration
{
    public string MainConfPath { get; init; } = string.Empty;
    public string CustomConfPath { get; init; } = string.Empty;
    public bool ForceFullscreen { get; init; } = true;
}