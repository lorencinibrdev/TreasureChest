using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Install;

public readonly record struct InstalledGameManifest(
    long GameId,
    string Slug,
    string InstallDirectoryPath,
    string ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    DateTime InstallDate,
    ERunnerType? RunnerType
);