using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchConfigs;

public record WineLaunchConfiguration : LaunchConfiguration
{
    public WineLaunchConfiguration() => RunnerType = ERunnerType.Wine;
    public string RunnerExecutable { get; init; } = "wine";
    public string PrefixPath { get; init; } = string.Empty;
    public bool EnableEsync { get; init; } = true;
    public bool EnableFsync { get; init; } = true;
}