using System.Text.Json;
using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Services.Install;

public class InstallerService
{
    // Private Members:
    private readonly JsonSerializerOptions _serializerOptions;
    
    private InstallerService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        Directory.CreateDirectory(ManifestsDirectory);
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

    public bool IsGameInstalled(string gameSlug)
    {
        return false;
    }

    public void RegisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.Slug}.json");
        var json = JsonSerializer.Serialize(manifest, _serializerOptions);
        File.WriteAllText(manifestPath, json);
    }

    public static (EGameInstallationType, string setupFilePath) IdentifyGameInstallationType(string sourceFolderPath)
    {
        if (!Directory.Exists(sourceFolderPath))
            return (EGameInstallationType.Unknown, string.Empty);
        
        // Checking for linux first:
        var linuxInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.sh")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith("gog_"));

        if (!string.IsNullOrWhiteSpace(linuxInstaller))
            return (EGameInstallationType.LinuxNative, linuxInstaller);
        
        // If not found, falling back to windows installs:
        var windowsInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.exe")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_"));

        if (!string.IsNullOrWhiteSpace(windowsInstaller))
            return (EGameInstallationType.WindowsInstaller, windowsInstaller);
        
        //TODO: Handle MacOS!
        return (EGameInstallationType.Unknown, string.Empty);
    }
    
    // Properties:
    public static InstallerService Instance { get; } = new();
    private string ManifestsDirectory => Path.Combine(SettingsService.DataDirectory, "InstallationManifests");
}