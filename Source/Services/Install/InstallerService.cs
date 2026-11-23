using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TreasureChest.Source.Helpers;
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
        
        // Preloading all JSON manifests available:
        foreach (var manifestJsonFilePath in Directory.GetFiles(ManifestsDirectory, "*.json"))
        {
            var jsonText = File.ReadAllText(manifestJsonFilePath);

            if (string.IsNullOrWhiteSpace(jsonText))
                continue;
            
            var manifestEntry = JsonSerializer.Deserialize<InstalledGameManifest>(jsonText, _serializerOptions);

            if (manifestEntry is null)
            {
                Console.Out.WriteLine($"[WARN] - Malformed InstallManifest Json structure on file: {manifestJsonFilePath}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(manifestEntry.GameSlug))
                continue;

            _manifestCache[manifestEntry.GameSlug] = manifestEntry;
        }
        
        Console.Out.WriteLine($"[INFO] - Pre-Loaded {_manifestCache.Count} json manifest files.");
    }

    public async Task<bool> InstallAsync(GameEntry gameEntry, DownloadManifest downloadManifest)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Requested installation of '{gameEntry.Title}'");

        if (!Directory.Exists(downloadManifest.DownloadDirectoryPath))
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Cannot install {gameEntry.Title}. Source files not found on: {downloadManifest.DownloadDirectoryPath}");
            return false;
        }
        
        if (!TryIdentifySetupFilePath(gameEntry.Slug, downloadManifest.DownloadDirectoryPath, out var setupFilePath))
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Cannot install {gameEntry.Title}. Setup file path could not be identified.");
            return false;
        }
        
        var installationType = IdentifySetupType(setupFilePath);

        if (installationType == EInstallationType.Unknown)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Cannot install {gameEntry.Title}. Install Type could not be identified from setup file: {setupFilePath}");
            return false;
        }
        
        var globalInstallDir = SettingsService.Instance.LibraryFolder;
        var targetFolderPath = Path.Combine(globalInstallDir, gameEntry.Slug);
        Directory.CreateDirectory(targetFolderPath);
        
        // Publishing the installation has started:
        LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installing);

        await Console.Out.WriteLineAsync($"[INFO] - Requested installation of '{gameEntry.Title}'");
        await Console.Out.WriteLineAsync($"[INFO] - Source folder for installation files: {downloadManifest.DownloadDirectoryPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Target folder for installation: {targetFolderPath}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified setup file: {setupFilePath}");
        await Console.Out.WriteLineAsync($"[INFO] - Identified installation type: {installationType.ToString()}");

        var installedSuccessfully = false;

        await Task.Run(async () =>
        {
            installedSuccessfully = installationType switch
            {
                EInstallationType.LinuxNative => await RunLinuxNativeInstallAsync(setupFilePath, targetFolderPath),
                EInstallationType.WindowsInstaller => await RunWindowsInstallAsync(setupFilePath, targetFolderPath),
                EInstallationType.MacOsInstaller => await RunMacOsInstallAsync(setupFilePath, targetFolderPath),
                _ => false
            };
        });

        if (installedSuccessfully)
        {
            // Parsing the .info files (if any):
            if (!TryParseInfoFiles(gameEntry, targetFolderPath, out var primaryTask, out var otherTasksList))
            {
                await Console.Out.WriteLineAsync("[ERROR] - Could not define a primary task for the installed game.");
                return false;
            }
         
            // Creating and saving the installation manifest:
            var manifest = new InstalledGameManifest
            {
                GameId = gameEntry.Id,
                GameSlug = gameEntry.Slug,
                InstallDirectoryPath = targetFolderPath,
                PrimaryTask = primaryTask,
                OtherTasks = otherTasksList
            };

            LaunchConfigurationService.Instance.CreateAndSaveLaunchConfiguration(manifest);
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

    private static bool TryIdentifySetupFilePath(string gameSlug, string downloadDirectoryPath,  [NotNullWhen(true)] out string? setupFilePath)
    {
        var linuxInstallerFile = Directory
            .EnumerateFiles(downloadDirectoryPath, "*.sh", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") 
                                 || Path.GetFileName(f).StartsWith("gog_") 
                                 || Path.GetFileName(f).StartsWith(gameSlug));

        if (!string.IsNullOrWhiteSpace(linuxInstallerFile))
        {
            setupFilePath = linuxInstallerFile;
            return true;
        }
        
        var windowsInstallerFile = Directory
            .EnumerateFiles(downloadDirectoryPath, "*.exe", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") 
                                 || Path.GetFileName(f).StartsWith(gameSlug));

        if (!string.IsNullOrWhiteSpace(windowsInstallerFile))
        {
            setupFilePath = windowsInstallerFile;
            return true;
        }
        
        var macOsInstallerFile = Directory
            .EnumerateFiles(downloadDirectoryPath, "*.dmg", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("setup_") 
                                 || Path.GetFileName(f).StartsWith(gameSlug));

        if (!string.IsNullOrWhiteSpace(macOsInstallerFile))
        {
            setupFilePath = macOsInstallerFile;
            return true;
        }
     
        Console.Out.WriteLine("[ERROR] - Installer file could not be identified.");
        setupFilePath = null;
        return false;
    }

    private static EInstallationType IdentifySetupType(string setupFilePath)
    {
        if (string.IsNullOrWhiteSpace(setupFilePath) || !File.Exists(setupFilePath))
            return EInstallationType.Unknown;

        // Checking the extension of the file:
        var extension = Path.GetExtension(setupFilePath);

        if (extension.Equals(".sh", StringComparison.OrdinalIgnoreCase))
            return EInstallationType.LinuxNative;
        
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return EInstallationType.WindowsInstaller;
        
        if (extension.Equals(".dmg", StringComparison.OrdinalIgnoreCase))
            return EInstallationType.MacOsInstaller;
        
        return EInstallationType.Unknown;
    }
    
    private static bool TryParseInfoFiles(GameEntry gameEntry, string installDirectoryPath,
        [NotNullWhen(true)] out GogPlayTaskEntry? primaryTask,
        [NotNullWhen(true)] out List<GogPlayTaskEntry>? otherTasks)
    {
        primaryTask = null;
        otherTasks = null;
        
        if (string.IsNullOrWhiteSpace(installDirectoryPath) || !Directory.Exists(installDirectoryPath))
            return false;
        
        var infoFilePath = Directory.GetFiles(installDirectoryPath, "goggame-*.info", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(infoFilePath))
        {
            try
            {
                var json = File.ReadAllText(infoFilePath);
                var gameInfo = JsonSerializer.Deserialize<GogGameInfo>(json);

                if (gameInfo is null || gameInfo.PlayTasks.Count == 0)
                    return false;

                primaryTask = gameInfo.PlayTasks.FirstOrDefault(t => t is { IsPrimary: true }) ?? new GogPlayTaskEntry();
                otherTasks = gameInfo.PlayTasks.FindAll(t => t is { IsPrimary: false });
                
                // Fixing the executable path and the working dir, to have the full path:
                var fullExecutablePath = Path.Join(installDirectoryPath, primaryTask.Path.Replace("\\", "/"));
                var fullWorkingDirectoryPath = Path.Join(installDirectoryPath, primaryTask.WorkingDirectory ?? string.Empty);

                primaryTask.Path = fullExecutablePath;
                primaryTask.WorkingDirectory = fullWorkingDirectoryPath;
            }
            catch (Exception e)
            {
                Console.Out.WriteLine($"[WARN] - Failed to parse GOG Info file: {e.Message}");
            }
        }
        
        if (primaryTask is null)
        {
            Console.Out.WriteLine("[WARN] - No .info file found after GOG finished installation. Falling back to manual identification of the launcher...");
            
            var executablePath = IdentifyGameLauncherFile(installDirectoryPath);
            
            Console.Out.WriteLine(string.IsNullOrWhiteSpace(executablePath)
                ? "[ERROR] - Could not identify the game launcher file..."
                : $"[INFO] - Identified Launch File: {executablePath}");
            
            // We don't have a .info file, we fall back to identifying the runner and executable by ourselves:
            primaryTask = new GogPlayTaskEntry
            {
                Name = $"Run {gameEntry.Title}",
                IsPrimary = true,
                Category = "game",
                IsHidden = false,
                Path = executablePath,
                WorkingDirectory = installDirectoryPath
            };
        }
        
        // Identifying and saving the runner type:
        var runnerType = IdentifyRunnerType(installDirectoryPath, primaryTask.Path);

        Console.Out.WriteLine(runnerType == ERunnerType.Unknown
            ? $"[ERROR] - Could not identify the Runner Type for the game launcher: {primaryTask.Path}"
            : $"[INFO] - Identified Runner Type: {runnerType.ToString()}");
        
        primaryTask.RunnerType = runnerType;
        
        otherTasks ??= [];
        return true;
    }
    
    private static string IdentifyGameLauncherFile(string installDirectoryPath)
    {
        var fallbackScript = Directory.GetFiles(installDirectoryPath, "*.sh", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(fallbackScript))
            return fallbackScript;
        
        // Fallback to windows executable:
        var fallbackExe = Directory.GetFiles(installDirectoryPath, "*.exe", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(fallbackExe))
            return fallbackExe;
        
        var fallbackDmg = Directory.GetFiles(installDirectoryPath, "*.dmg", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(fallbackDmg))
            return fallbackDmg;
        
        return string.Empty;
    }
    
    private static ERunnerType IdentifyRunnerType(string installDirectoryPath, string executableFilePath)
    {
        if (string.IsNullOrWhiteSpace(installDirectoryPath) || string.IsNullOrWhiteSpace(executableFilePath))
            return ERunnerType.Unknown;
        
        // Searching for a DOSBOX folder in the installation directory, or a windows bundled dosbox executable:
        if (Directory.Exists(Path.Combine(installDirectoryPath, "dosbox")) 
            || Directory.Exists(Path.Combine(installDirectoryPath, "DOSBOX"))
            || executableFilePath.Contains("dosbox.exe", StringComparison.OrdinalIgnoreCase))
            return ERunnerType.DosBox;
        
        var scummvmFolderPath = Path.Combine(installDirectoryPath, "scummvm");
        if (Directory.Exists(scummvmFolderPath) || executableFilePath.Contains("scummvm", StringComparison.OrdinalIgnoreCase))
            return ERunnerType.ScummVm;

        // If the executable is a .exe file, then we go with Wine:
        if (executableFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || executableFilePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            return ERunnerType.Wine;
     
        if (string.IsNullOrWhiteSpace(executableFilePath) || 
            !File.Exists(executableFilePath) || 
            string.IsNullOrWhiteSpace(installDirectoryPath))
            return ERunnerType.Unknown;
        
        return ERunnerType.Native;
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
            if (Directory.Exists(gameManifest.InstallDirectoryPath))
            {
                await Task.Run(() => Directory.Delete(gameManifest.InstallDirectoryPath, true));
                await Console.Out.WriteLineAsync($"[INFO] - Removed game files at {gameManifest.InstallDirectoryPath}...");
            }
            
            // Deleting the JSON manifest from the disk and from the cache:
            UnregisterInstallation(gameManifest);

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
    
    private static async Task<bool> RunLinuxNativeInstallAsync(string setupFilePath, string targetFolderPath)
    {
        var tempExtractPath = Path.Combine(Path.GetTempPath(), $"TreasureChest_Install_{Guid.NewGuid()}");

        if (!RunnerManagerService.IsCommandAvailable("unzip"))
        {
            await Console.Out.WriteLineAsync("[ERROR] - 'unzip' is not installed on this system. Cannot proceed.");
            return false;
        }
        
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

            processStartInfo.ArgumentList.Add("-q"); // Quiet
            processStartInfo.ArgumentList.Add("-o"); // Overwrite
            processStartInfo.ArgumentList.Add(setupFilePath);
            processStartInfo.ArgumentList.Add("-d");
            processStartInfo.ArgumentList.Add(tempExtractPath);

            await Console.Out.WriteLineAsync("[INFO] - Unzipping the game data (Bypassing MojoSetup)...");

            using var process = Process.Start(processStartInfo);

            if (process is null)
                return false;

            _ = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                var stderr = await stdErrTask;

                Console.WriteLine($"[ERROR] - Linux Native Install exited with code {process.ExitCode}");

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.WriteLine($"[ERROR] - StdErr: {stderr}");
                }

                return false;
            }

            await Console.Out.WriteLineAsync(
                "[INFO] - Successfully extracted game data. Trying to identify the actual game information...");

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
            else
            {
                await Console.Out.WriteLineAsync("[ERROR] - Could not identify the game payload directory structure.");
                return false;
            }

            MoveDirectoryContents(gamePayload, targetFolderPath, true);
            MakeGameBinariesExecutable(targetFolderPath);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Linux Native Install failed: {e.Message}");
            return false;
        }
        finally
        {
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);    
            }
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
            
            _ = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await stdErrTask;
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
        // TODO: Handle macOS install?
        throw new NotImplementedException("[ERROR] - MacOS installation is not implemented.");
    }

    private static void MakeGameBinariesExecutable(string targetDir)
    {
        var scripts = Directory.GetFiles(targetDir, "*.sh", SearchOption.AllDirectories);
        var binaries = Directory.GetFiles(targetDir, "*.x86_64", SearchOption.AllDirectories);

        var allExecutables = scripts.Concat(binaries);

        foreach (var executable in allExecutables)
        {
            try
            {
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | 
                                                 UnixFileMode.GroupRead | UnixFileMode.GroupExecute | 
                                                 UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public bool TryGetInstallManifest(string gameSlug, [NotNullWhen(true)] out InstalledGameManifest? installManifest)
    {
        installManifest = null;
        
        if (string.IsNullOrWhiteSpace(gameSlug))
            return false;

        if (!_manifestCache.TryGetValue(gameSlug, out var foundManifest)) 
            return false;
        
        installManifest = foundManifest;
        return true;
    }

    private void RegisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.GameSlug}.json");
        var json = JsonSerializer.Serialize(manifest, _serializerOptions);
        File.WriteAllText(manifestPath, json);
        _manifestCache[manifest.GameSlug] = manifest;
    }
    
    private void UnregisterInstallation(InstalledGameManifest manifest)
    {
        var manifestPath = Path.Combine(ManifestsDirectory, $"{manifest.GameSlug}.json");

        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
            Console.Out.WriteLine($"[INFO] - Removed manifest file at {manifestPath}...");
        }
        
        _manifestCache.Remove(manifest.GameSlug);
    }
    
    private static void MoveSupportFiles(string installFolder)
    {
        var supportFolderRootPath = Path.Combine(installFolder, "__support");
        
        if (!Directory.Exists(supportFolderRootPath))
            return;
        
        string[] supportSubFolders = ["app", "save", "add"];

        foreach (var subFolder in supportSubFolders)
        {
            var supportFolderPath = Path.Combine(supportFolderRootPath, subFolder);

            if (!Directory.Exists(supportFolderPath))
                continue;

            // First, moving subdirectories: 
            foreach (var subDir in Directory.GetDirectories(supportFolderPath))
            {
                var dirName = Path.GetFileName(subDir);
                var targetDir = !string.IsNullOrWhiteSpace(dirName) ? Path.Combine(installFolder, dirName) : installFolder;
                Directory.CreateDirectory(targetDir);
                MoveDirectoryContents(subDir, targetDir, false);
            }
        }
        
        // Moving any DOSBOX-related configs (if any):
        var dosBoxFolder = Directory.Exists(Path.Combine(installFolder, "DOSBOX"))
            ? Path.Combine(installFolder, "DOSBOX")
            : Directory.Exists(Path.Combine(installFolder, "dosbox"))
            ? Path.Combine(installFolder, "dosbox")
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(dosBoxFolder))
        {
            var dosboxConfigs = Directory.GetFiles(supportFolderRootPath, "*.conf", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).StartsWith("dosbox", StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            foreach (var dosboxConfigFile in dosboxConfigs)
            {
                var targetPath = Path.Combine(dosBoxFolder, Path.GetFileName(dosboxConfigFile));
            
                if (File.Exists(targetPath))
                    continue;
            
                File.Move(dosboxConfigFile, targetPath);
            }
        }
        
        // Moving any remaining file to the root folder:
        foreach (var remainingFile in Directory.GetFiles(supportFolderRootPath))
        {
            var targetPath = Path.Combine(installFolder, Path.GetFileName(remainingFile));
            
            if (File.Exists(targetPath))
                continue;
            
            File.Move(remainingFile, targetPath);
        }
        
        // Finally, some GOG older games have data in the 'app/' folder:
        var rootAppFolder = Path.Combine(installFolder, "app");

        if (Directory.Exists(rootAppFolder))
        {
            MoveDirectoryContents(rootAppFolder, installFolder, false);
        }
    }
    
    private static void MoveDirectoryContents(string sourceDirectoryPath, string targetDirectoryPath, bool overwrite)
    {
        foreach (var filePath in Directory.GetFiles(sourceDirectoryPath))
        {
            var destinationPath = Path.Combine(targetDirectoryPath, Path.GetFileName(filePath));
            File.Move(filePath, destinationPath, overwrite);
        }

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectoryPath))
        {
            var destinationPath = Path.Combine(targetDirectoryPath, Path.GetFileName(directoryPath));
            Directory.CreateDirectory(destinationPath);
            MoveDirectoryContents(directoryPath, destinationPath, overwrite);
        }
    }
    
    // Properties:
    public static InstallerService Instance { get; } = new();
    private static string ManifestsDirectory => Path.Combine(SettingsService.DataDirectory, "InstallationManifests");
}