using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Services.Install;

public class InstallerService
{
    private InstallerService()
    {
        
    }

    public async Task<bool> InstallAsync(GogResponseGameDetails gameInformation, string sourceFolderPath)
    {
        var gameSlug = Path.GetDirectoryName(sourceFolderPath);
        var globalInstallDir = SettingsService.Instance.LibraryFolder;

        if (string.IsNullOrWhiteSpace(gameSlug) || string.IsNullOrWhiteSpace(globalInstallDir))
            return false;

        var (installationType, setupFilePath) = IdentifyGameInstallationType(sourceFolderPath);
        
        var targetFolderPath = Path.Combine(globalInstallDir, gameSlug);
        
        Directory.CreateDirectory(targetFolderPath);

        await Console.Out.WriteLineAsync($"[INFO] - Requested installation of '{gameInformation.Title}'");
        await Console.Out.WriteLineAsync($"[INFO] - Source folder for installation files: {sourceFolderPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Target folder for installation: {targetFolderPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified installation type: {installationType.ToString()}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified setup file: {setupFilePath}");

        return true;
    }

    private (EGameInstallationType, string setupFilePath) IdentifyGameInstallationType(string sourceFolderPath)
    {
        if (!Directory.Exists(sourceFolderPath))
            return (EGameInstallationType.Unknown, string.Empty);
        
        // Checking for linux first:
        var linuxInstallers = Directory.EnumerateFiles(sourceFolderPath, "*.sh")
            .Where(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith("gog_"))
            .ToList();

        if (linuxInstallers.Count != 0)
            return (EGameInstallationType.LinuxNative, linuxInstallers.First());
        
        // If not found, falling back to windows installs:
        var windowsInstallers = Directory.EnumerateFiles(sourceFolderPath, "*.exe")
            .Where(f => Path.GetFileName(f).StartsWith("setup_"))
            .ToList();

        if (windowsInstallers.Count != 0)
            return (EGameInstallationType.WindowsInstaller, windowsInstallers.First());
        
        //TODO: Handle MacOS!
        return (EGameInstallationType.Unknown, string.Empty);
    }
    
    // Properties:
    public static InstallerService Instance { get; } = new();
}