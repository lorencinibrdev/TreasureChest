using System.Diagnostics;
using System.Text.Json;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Install;

public class InstallerService
{
    // Private Members:
    private readonly Dictionary<string, InstalledGameManifest> _manifestCache = [];
    private readonly JsonSerializerOptions _serializerOptions;
    
    private InstallerService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        Directory.CreateDirectory(ManifestsDirectory);
        
        // Pre-loading all json manifests available:
        foreach (var manifestJsonFiles in Directory.GetFiles(ManifestsDirectory, "*.json"))
        {
            var jsonText = File.ReadAllText(manifestJsonFiles);

            if (string.IsNullOrWhiteSpace(jsonText))
                continue;
            
            var manifestEntry = JsonSerializer.Deserialize<InstalledGameManifest>(jsonText, _serializerOptions);

            if (string.IsNullOrWhiteSpace(manifestEntry.Slug))
                continue;

            if (manifestEntry.RunnerType is null)
            {
                var runner = IdentifyRunnerType(manifestEntry.InstallDirectoryPath, manifestEntry.ExecutablePath);
                manifestEntry = manifestEntry with { RunnerType = runner };
                RegisterInstallation(manifestEntry);
            }

            _manifestCache[manifestEntry.Slug] = manifestEntry;
        }
        
        Console.Out.WriteLine($"[INFO] - Pre-Loaded {_manifestCache.Count} json manifest files.");
    }

    public async Task<bool> InstallAsync(GameEntry gameEntry, string sourceFolderPath)
    {
        var globalInstallDir = SettingsService.Instance.LibraryFolder;

        if (string.IsNullOrWhiteSpace(gameEntry.Slug) || string.IsNullOrWhiteSpace(globalInstallDir))
            return false;

        var (installationType, setupFilePath) = IdentifyGameInstallationType(gameEntry, sourceFolderPath);

        if (installationType == EGameInstallationType.Unknown)
            return false;
        
        var targetFolderPath = Path.Combine(globalInstallDir, gameEntry.Slug);
        Directory.CreateDirectory(targetFolderPath);
        
        // publishing the instalataion has started:
        LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installing);

        await Console.Out.WriteLineAsync($"[INFO] - Requested installation of '{gameEntry.Title}'");
        await Console.Out.WriteLineAsync($"[INFO] - Source folder for installation files: {sourceFolderPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Target folder for installation: {targetFolderPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified installation type: {installationType.ToString()}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified setup file: {setupFilePath}");

        var installedSuccessfully = false;

        await Task.Run(async () =>
        {
            installedSuccessfully = installationType switch
            {
                EGameInstallationType.Unknown => false,
                EGameInstallationType.LinuxNative => await RunLinuxNativeInstallAsync(setupFilePath, targetFolderPath),
                EGameInstallationType.WindowsInstaller => await RunWindowsInstallAsync(setupFilePath, targetFolderPath),
                EGameInstallationType.MacOsInstaller => await RunMacOsInstallAsync(setupFilePath, targetFolderPath),
                _ => throw new ArgumentOutOfRangeException()
            };
        });

        if (installedSuccessfully)
        {
            var executablePath = FindExecutablePath(targetFolderPath);

            await Console.Out.WriteLineAsync($"[INFO] - Resolved game executable: {executablePath}");

            var runner = IdentifyRunnerType(targetFolderPath, executablePath);
            
            var manifest = new InstalledGameManifest(
                GameId: gameEntry.Id,
                Slug: gameEntry.Slug,
                InstallDirectoryPath: targetFolderPath,
                ExecutablePath: executablePath,
                InstallDate: DateTime.UtcNow,
                RunnerType: runner
            );

            RegisterInstallation(manifest);
            
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installed);
        }
        else
        {
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Downloaded);
            await Console.Out.WriteLineAsync($"[ERROR] - Installation failed for '{gameEntry.Title}'");
        }

        return installedSuccessfully;
    }
    
    private static ERunnerType IdentifyRunnerType(string installedFolderPath, string executablePath)
    {
        // If the executable is a .exe file, then we go with Wine:
        if (executablePath.EndsWith(".exe") || executablePath.EndsWith(".msi"))
            return ERunnerType.Wine;
        
        // Searching for a DOSBOX folder in the installation directory:
        var dosboxFolderPath = Path.Combine(installedFolderPath, "dosbox");

        if (Directory.Exists(dosboxFolderPath))
            return ERunnerType.DosBox;

        var scummvmFolderPath = Path.Combine(installedFolderPath, "scummvm");
        
        return Directory.Exists(scummvmFolderPath) 
            ? ERunnerType.ScummVm 
            : ERunnerType.Native;
    }

    private static async Task<bool> RunLinuxNativeInstallAsync(string setupFilePath, string targetFolderPath)
    {
        var tempExtractPath = Path.Combine(Path.GetTempPath(), $"TreasureChest_Install_{Guid.NewGuid()}");
        
        try
        {
            Directory.CreateDirectory(tempExtractPath);
            
            var processStartInfo = new ProcessStartInfo
            {       
                FileName = "unzip",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            processStartInfo.ArgumentList.Add("-q");    // Quiet
            processStartInfo.ArgumentList.Add("-o");    // Overwrite
            processStartInfo.ArgumentList.Add(setupFilePath);
            processStartInfo.ArgumentList.Add("-d");
            processStartInfo.ArgumentList.Add(tempExtractPath);

            await Console.Out.WriteLineAsync("[INFO] - Unzipping the game data (Bypassing MojoSetup)...");
            
            using var process = Process.Start(processStartInfo);

            if (process is null)
                return false;
            
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                Console.WriteLine($"[ERROR] - Linux Native Install exited with code {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"[ERROR] - StdErr: {stderr}");
                return false;
            }

            await Console.Out.WriteLineAsync("[INFO] - Successfully extracted game data. Trying to identify the actual game information...");

            var noarchDirPath = Path.Combine(tempExtractPath, "data", "noarch");
            var gamePayload = Path.Combine(noarchDirPath, "game");
            
            // Modern GOG titles:
            if (Directory.Exists(gamePayload))
            {
                await Console.Out.WriteLineAsync("[INFO] - Detected modern GOG payload structure.");
            }
            else if (Directory.Exists(noarchDirPath) && File.Exists(Path.Combine(noarchDirPath, "start.sh")))
            {
                gamePayload = noarchDirPath;
                await Console.Out.WriteLineAsync("[INFO] - Detected legacy/DOSBOX GOG payload structure.");
            }
            else if (Directory.Exists(noarchDirPath) && File.Exists(Path.Combine(noarchDirPath, "start.sh")))
            {
                await Console.Out.WriteLineAsync("[ERROR] - Could not identify the game payload directory structure.");
                return false;
            }
            
            MoveDirectoryContents(gamePayload, targetFolderPath);
            MakeGameBinariesExecutable(targetFolderPath);
            
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Linux Native Install failed: {e.Message}");
            return false;
        }
    }
    
    private static async Task<bool> RunWindowsInstallAsync(string setupFilePath, string targetFolderPath)
    {
        return false;
    }
    
    private static async Task<bool> RunMacOsInstallAsync(string setupFilePath, string targetFolderPath)
    {
        return false;
    }

    private static void MoveDirectoryContents(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));

            if (File.Exists(dest))
            {
                File.Delete(dest);
            }
            
            File.Move(file, dest);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(directory));
            Directory.CreateDirectory(dest);
            MoveDirectoryContents(directory, dest);
        }
    }

    private static void MakeGameBinariesExecutable(string targetDir)
    {
        var scripts = Directory.GetFiles(targetDir, "*.sh", SearchOption.AllDirectories);
        var binaries = Directory.GetFiles(targetDir, "*.x86_64", SearchOption.AllDirectories);

        var allExecutables = scripts.Concat(binaries);

        foreach (var executable in allExecutables)
        {
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | 
                                             UnixFileMode.GroupRead | UnixFileMode.GroupExecute | 
                                             UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    public bool IsGameInstalled(string gameSlug)
    {
        if (string.IsNullOrWhiteSpace(gameSlug))
            return false;

        return _manifestCache.TryGetValue(gameSlug, out var manifestEntry) &&
               Directory.Exists(manifestEntry.InstallDirectoryPath);
    }
    
    public InstalledGameManifest? GetGameManifest(string gameSlug)
    {
        if (!IsGameInstalled(gameSlug))
            return null;

        if (!_manifestCache.TryGetValue(gameSlug, out var manifestEntry))
            return null;
        
        return manifestEntry;
    }

    private void RegisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.Slug}.json");
        var json = JsonSerializer.Serialize(manifest, _serializerOptions);
        File.WriteAllText(manifestPath, json);
        
        _manifestCache[manifest.Slug] = manifest;
    }

    public static (EGameInstallationType installationType, string setupFilePath) IdentifyGameInstallationType(GameEntry gameEntry, string sourceFolderPath)
    {
        if (!Directory.Exists(sourceFolderPath))
            return (EGameInstallationType.Unknown, string.Empty);
        
        // Checking for linux first:
        var linuxInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.sh")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith("gog_") || Path.GetFileName(f).StartsWith(gameEntry.Slug));

        if (!string.IsNullOrWhiteSpace(linuxInstaller))
            return (EGameInstallationType.LinuxNative, linuxInstaller);
        
        // If not found, falling back to windows installs:
        var windowsInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.exe")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith(gameEntry.Slug));

        if (!string.IsNullOrWhiteSpace(windowsInstaller))
            return (EGameInstallationType.WindowsInstaller, windowsInstaller);
        
        //TODO: Handle MacOS!
        return (EGameInstallationType.Unknown, string.Empty);
    }

    private string FindExecutablePath(string targetFolderPath)
    {
        var infoFiles = Directory.GetFiles(targetFolderPath, "goggame-*.info");

        if (infoFiles.Length != 0)
        {
            try
            {
                var json = File.ReadAllText(infoFiles[0]);
                var gameInfo = JsonSerializer.Deserialize<GogGameInfo>(json, _serializerOptions);

                var primaryTask = gameInfo?.PlayTasks.FirstOrDefault(t => t is { IsPrimary: true, Category: "game" });

                if (primaryTask is not null && !string.IsNullOrWhiteSpace(primaryTask.Path))
                {
                    return Path.Combine(targetFolderPath, primaryTask.Path);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine($"[WARN] - Failed to parse GOG Info file: {e.Message}");
            }
        }
        
        var fallbackScript = Directory.GetFiles(targetFolderPath, "*.sh")
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        if (fallbackScript is not null)
            return fallbackScript;
        
        // Fallback to windows executable:
        var fallbackExe = Directory.GetFiles(targetFolderPath, "*.exe")
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        return fallbackExe ?? string.Empty;
    }
    
    // Properties:
    public static InstallerService Instance { get; } = new();
    private static string ManifestsDirectory => Path.Combine(SettingsService.DataDirectory, "InstallationManifests");
}