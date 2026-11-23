using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchConfigs;

public record NativeLaunchConfiguration : LaunchConfiguration
{
    public NativeLaunchConfiguration() => RunnerType = ERunnerType.Native;
    public bool RunInTerminal { get; init; }
}