namespace TreasureChest.Source.Services.Runners;

public enum ERunnerType
{
    DosBox,
    ScummVm,
    Wine,
    Proton
}

public class RunnerInfo
{
    // Properties:
    public ERunnerType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public bool IsSystemInstalled { get; set; }
    public bool IsAvailable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
}