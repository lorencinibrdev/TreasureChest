using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchConfigs;

public abstract record LaunchConfiguration
{
    public required ERunnerType RunnerType { get; init; }
    public string ExecutablePath { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string CustomArguments { get; init; } = string.Empty;
}