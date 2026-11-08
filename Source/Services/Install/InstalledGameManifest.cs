using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Install;

public readonly record struct InstalledGameManifest(
    long GameId,
    string Slug,
    string InstallDirectoryPath,
    string ExecutablePath,
    DateTime InstallDate,
    ERunnerType? RunnerType
);