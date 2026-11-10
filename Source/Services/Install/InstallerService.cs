using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Downloads;
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

        var (installationType, setupFilePath) = IdentifyGameInstallationType(gameEntry.Slug, sourceFolderPath);

        if (installationType == EGameInstallationType.Unknown)
            return false;
        
        var targetFolderPath = Path.Combine(globalInstallDir, gameEntry.Slug);
        Directory.CreateDirectory(targetFolderPath);
        
        // publishing the installation has started:
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
            var (executablePath, arguments, workingDir) = FindExecutablePath(targetFolderPath);

            await Console.Out.WriteLineAsync($"[INFO] - Resolved game executable: {executablePath}");

            var runner = IdentifyRunnerType(targetFolderPath, executablePath);

            var manifest = new InstalledGameManifest(
                GameId: gameEntry.Id,
                Slug: gameEntry.Slug,
                InstallDirectoryPath: targetFolderPath,
                ExecutablePath: executablePath,
                Arguments: arguments,
                WorkingDirectory: workingDir,
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

    public async Task<bool> UninstallAsync(GameEntry gameEntry)
    {
        if (!TryGetInstallManifest(gameEntry.Slug, out var gameManifest))
        {
            await Console.Out.WriteLineAsync($"[WARN] - Requested to uninstall {gameEntry.Slug}, but manifest is not present for it.");
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Available);
            return true;
        }
        
        LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Uninstalling);
        await Console.Out.WriteLineAsync($"[INFO] - Uninstalling {gameEntry.Slug}...");

        try
        {
            // Deleting the game directory:
            if (Directory.Exists(gameManifest.Value.InstallDirectoryPath))
            {
                await Task.Run(() => Directory.Delete(gameManifest.Value.InstallDirectoryPath, true));
                await Console.Out.WriteLineAsync($"[INFO] - Removed game files at {gameManifest.Value.InstallDirectoryPath}...");
            }
            
            // Deleting the JSON manifest from the disk and from the cache:
            UnregisterInstallation(gameManifest.Value);

            var newGameState = DownloadQueueService.Instance.IsGameDownloaded(gameEntry.Slug)
                ? EGameState.Downloaded
                : EGameState.Available;
            
            LibraryService.Instance.SetGameState(gameEntry.Id, newGameState);
            
            await Console.Out.WriteLineAsync($"[INFO] - Uninstallation completed. New game state: {newGameState}...");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Failed to uninstall {gameEntry.Title}: {e.Message}");
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installed);
            return false;
        }
    }
    
    private static ERunnerType IdentifyRunnerType(string installedFolderPath, string executablePath)
    {
        // Searching for a DOSBOX folder in the installation directory:
        if (Directory.Exists(Path.Combine(installedFolderPath, "dosbox")) 
            || Directory.Exists(Path.Combine(installedFolderPath, "DOSBOX"))
            || executablePath.Contains("dosbox.exe", StringComparison.OrdinalIgnoreCase))
            return ERunnerType.DosBox;
        
        var scummvmFolderPath = Path.Combine(installedFolderPath, "scummvm");
        if (Directory.Exists(scummvmFolderPath))
            return ERunnerType.ScummVm;

        // If the executable is a .exe file, then we go with Wine:
        if (executablePath.EndsWith(".exe") || executablePath.EndsWith(".msi"))
            return ERunnerType.Wine;
        
        return ERunnerType.Native;
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
        // Using innoextract to extract the game data:
        var innoRunner = RunnerManagerService.GetRunnerInformation(ERunnerType.InnoExtract);

        if (string.IsNullOrWhiteSpace(innoRunner.ExecutablePath) || !File.Exists(innoRunner.ExecutablePath))
        {
            Console.WriteLine("[INFO] - InnoExtract runner not found. Attempting to download...");

            var downloadUrl = RunnerManagerService.GetDownloadUrlForRunner(ERunnerType.InnoExtract);
            var installed = await RunnerManagerService.Instance.InstallRunnerAsync(ERunnerType.InnoExtract, downloadUrl);

            if (!installed)
            {
                Console.WriteLine("[ERROR] - InnoExtract installation failed.");
                return false;
            }

            innoRunner = RunnerManagerService.GetRunnerInformation(ERunnerType.InnoExtract);
        }
        
        Directory.CreateDirectory(targetFolderPath);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = innoRunner.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        processStartInfo.ArgumentList.Add("-s");
        processStartInfo.ArgumentList.Add("-d");
        processStartInfo.ArgumentList.Add(targetFolderPath);
        processStartInfo.ArgumentList.Add(setupFilePath);
        
        await Console.Out.WriteLineAsync("[INFO] - Extracting Windows game files using InnoExtract...");

        try
        {
            using var process = Process.Start(processStartInfo);

            if (process is null)
            {
                await Console.Out.WriteLineAsync("[ERROR] - Failed to run InnoExtract...");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                Console.WriteLine($"[ERROR] - Windows installer extraction exited with code {process.ExitCode}");
                
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.WriteLine($"[ERROR] - StdErr: {stderr}");
                }
                
                return false;
            }
            
            await Console.Out.WriteLineAsync("[INFO] - Successfully extracted Windows game files.");
            
            MoveSupportFiles(targetFolderPath);
            
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Windows InnoExtract install failed: {e.Message}");
            return false;
        }
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

    public bool TryGetInstallManifest(string gameSlug, [NotNullWhen(true)] out InstalledGameManifest? installManifest)
    {
        if (string.IsNullOrWhiteSpace(gameSlug))
        {
            installManifest = null;
            return false;
        }
        
        if (_manifestCache.TryGetValue(gameSlug, out var foundManifest))
        {
            installManifest = foundManifest;
            return true;
        }

        installManifest = null;
        return false;
    }

    private void RegisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.Slug}.json");
        var json = JsonSerializer.Serialize(manifest, _serializerOptions);
        File.WriteAllText(manifestPath, json);
        
        _manifestCache[manifest.Slug] = manifest;
    }
    
    private void UnregisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.Slug}.json");

        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
            Console.Out.WriteLine($"[INFO] - Removed manifest file at {manifestPath}...");
        }
        
        _manifestCache.Remove(manifest.Slug);
    }

    public static (EGameInstallationType installationType, string setupFilePath) IdentifyGameInstallationType(string gameSlug, string sourceFolderPath)
    {
        if (!Directory.Exists(sourceFolderPath))
            return (EGameInstallationType.Unknown, string.Empty);
        
        // Checking for linux first:
        var linuxInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.sh")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith("gog_") || Path.GetFileName(f).StartsWith(gameSlug));

        if (!string.IsNullOrWhiteSpace(linuxInstaller))
            return (EGameInstallationType.LinuxNative, linuxInstaller);
        
        // If not found, falling back to windows installs:
        var windowsInstaller = Directory
            .EnumerateFiles(sourceFolderPath, "*.exe")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") || Path.GetFileName(f).StartsWith(gameSlug));

        if (!string.IsNullOrWhiteSpace(windowsInstaller))
            return (EGameInstallationType.WindowsInstaller, windowsInstaller);
        
        //TODO: Handle MacOS!
        return (EGameInstallationType.Unknown, string.Empty);
    }

    private (string ExecutablePath, string? Arguments, string? WorkingDir) FindExecutablePath(string targetFolderPath)
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
                    var executablePath = Path.Combine(targetFolderPath, primaryTask.Path); 
                    var arguments = primaryTask.Arguments;
                    var workingDir = Path.Join(targetFolderPath, primaryTask.WorkingDir);
                    return (executablePath, arguments, workingDir);
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
            return (fallbackScript, null, null);
        
        // Fallback to windows executable:
        var fallbackExe = Directory.GetFiles(targetFolderPath, "*.exe")
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        if (fallbackExe is not null)
            return (fallbackExe, null, null);
        
        return (string.Empty, null, null);
    }

    private static void MoveSupportFiles(string installFolder)
    {
        var supportFolderPath = Path.Combine(installFolder, "__support", "app");
        
        if (!Directory.Exists(supportFolderPath))
            return;
        
        // // First, moving subdirectories:
        var subdirs = Directory.GetDirectories(supportFolderPath);
        
        foreach (var subdir in subdirs)
        {
            var dirName = Path.GetFileName(subdir);
            var targetDir = !string.IsNullOrWhiteSpace(dirName) ? Path.Combine(installFolder, dirName) : installFolder;
            MoveDirectoryContents(subdir, targetDir);    
        }
        
        // Now, moving special GOG Files (If Any):
        var dosBoxFolder = string.Empty;

        if (Directory.Exists(Path.Combine(installFolder, "DOSBOX")))
        {
            dosBoxFolder = Path.Combine(installFolder, "DOSBOX");
        }
        else if (Directory.Exists(Path.Combine(installFolder, "dosbox")))
        {
            dosBoxFolder = Path.Combine(installFolder, "dosbox");
        }

        if (string.IsNullOrWhiteSpace(dosBoxFolder))
            return;
        
        var dosboxConfigs = Directory.GetFiles(supportFolderPath, "*.conf")
            .Where(f => Path.GetFileName(f).StartsWith("dosbox", StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        foreach (var dosboxConfigFile in dosboxConfigs)
        {
            var targetPath = Path.Combine(dosBoxFolder, Path.GetFileName(dosboxConfigFile));
            
            if (File.Exists(targetPath))
                continue;
            
            File.Move(dosboxConfigFile, targetPath);
        }
        
        // Finally, moving any remaining file to the root folder:
        var remainingFiles = Directory.GetFiles(supportFolderPath);

        foreach (var remainingFile in remainingFiles)
        {
            var targetPath = Path.Combine(installFolder, Path.GetFileName(remainingFile));
            
            if (File.Exists(targetPath))
                continue;
            
            File.Move(remainingFile, targetPath);
        }
    }
    
    // Properties:
    public static InstallerService Instance { get; } = new();
    private static string ManifestsDirectory => Path.Combine(SettingsService.DataDirectory, "InstallationManifests");
}