using System.Diagnostics;
using System.Text.RegularExpressions;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher;

public partial class LauncherService
{
    private LauncherService()
    {
        
    }

    public async Task LaunchGameAsync(GameEntry game)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Launching game '{game.Title}' (Native)");

        if (!InstallerService.Instance.TryGetInstallManifest(game.Slug, out var gameManifest) 
            || string.IsNullOrWhiteSpace(gameManifest.Value.ExecutablePath))
        {
            Console.WriteLine($"[ERROR] - Cannot launch {game.Title}. Executable not found.");
            return;
        }

        ProcessStartInfo? processStartInfo = null;

        switch (gameManifest.Value.RunnerType)
        {
            case ERunnerType.Native:
                processStartInfo = CreateNativeProcessStartInfo(gameManifest.Value);
                break;
            case ERunnerType.DosBox:
                processStartInfo = await CreateDosboxProcessStartInfo(gameManifest.Value);
                break;
            case ERunnerType.ScummVm:
                // processStartInfo = CreateNativeProcessStartInfo(manifest.Value);
                break;
            case ERunnerType.Wine:
                processStartInfo = await CreateWineProcessStartInfo(gameManifest.Value);
                break;
            case ERunnerType.Proton:
                // processStartInfo = CreateNativeProcessStartInfo(manifest.Value);
                break;
            default:
                Console.WriteLine($"[ERROR] - Cannot launch {game.Title}. Bad launcher configured.");
                return;
        }

        if (processStartInfo is null)
            return;

        try
        {
            using var process = new Process();
            process.StartInfo = processStartInfo;

            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Console.Out.WriteLine($"[GAME_LOG] ({game.Slug}) - {args.Data}");
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Console.Out.WriteLine($"[GAME_LOG] ({game.Slug}) - {args.Data}");
                }
            };

            LibraryService.Instance.SetGameState(game.Id, EGameState.Running);

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            await Console.Out.WriteLineAsync($"[INFO] - Game '{game.Title}' exited with code {process.ExitCode}");
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to launch {game.Title}. Error: {e.Message}");
        }
        finally
        {
            LibraryService.Instance.SetGameState(game.Id, EGameState.Installed);   
        }
    }

    private static ProcessStartInfo CreateNativeProcessStartInfo(InstalledGameManifest manifest)
    {
        var workingDirectory = Path.GetDirectoryName(manifest.ExecutablePath) ?? manifest.InstallDirectoryPath;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = manifest.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        return processStartInfo;
    }
    
    private static async Task<ProcessStartInfo> CreateDosboxProcessStartInfo(InstalledGameManifest manifest)
    {
        var runnerInfo = RunnerManagerService.GetRunnerInformation(ERunnerType.DosBox);

        if (string.IsNullOrWhiteSpace(runnerInfo.ExecutablePath) || !File.Exists(runnerInfo.ExecutablePath))
        {
            Console.WriteLine("[INFO] - DOSBox runner not found. Attempting to download...");

            var downloadUrl = RunnerManagerService.GetDownloadUrlForRunner(ERunnerType.DosBox);
            var installed = await RunnerManagerService.Instance.InstallRunnerAsync(ERunnerType.DosBox, downloadUrl);

            if (!installed)
            {
                Console.WriteLine("[ERROR] - DOSBox installation failed. Falling back to GOG's Bundled DOSBox...");
                return CreateNativeProcessStartInfo(manifest);
            }

            runnerInfo = RunnerManagerService.GetRunnerInformation(ERunnerType.DosBox);
        }
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = runnerInfo.ExecutablePath,
            WorkingDirectory = manifest.WorkingDirectory ?? manifest.InstallDirectoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        
        // Finding the GOG's config files:
        var configFiles = FindDosboxConfigFiles(manifest);

        if (configFiles.Count == 0)
        {
            Console.WriteLine("[ERROR] - Could not find the '.conf' files in the game directory. Trying to run anyway...");
        }
        else
        {
            foreach (var configFile in configFiles)
            {
                processStartInfo.ArgumentList.Add("-conf");
                processStartInfo.ArgumentList.Add(configFile);
            }
        }

        processStartInfo.ArgumentList.Add("-fullscreen");

        return processStartInfo;
    }
    
    private static async Task<ProcessStartInfo?> CreateWineProcessStartInfo(InstalledGameManifest manifest)
    {
        var runnerInfo = RunnerManagerService.GetRunnerInformation(ERunnerType.Wine);

        if (string.IsNullOrWhiteSpace(runnerInfo.ExecutablePath) || !File.Exists(runnerInfo.ExecutablePath))
        {
            Console.WriteLine("[INFO] - WINE (Kron4ek) runner not found. Attempting to download...");

            var downloadUrl = RunnerManagerService.GetDownloadUrlForRunner(ERunnerType.Wine);
            var installed = await RunnerManagerService.Instance.InstallRunnerAsync(ERunnerType.Wine, downloadUrl);

            if (!installed)
            {
                Console.WriteLine("[ERROR] - WINE (Kron4ek) installation failed.");
                return null;
            }

            runnerInfo = RunnerManagerService.GetRunnerInformation(ERunnerType.Wine);
        }

        var workingDirectory = Path.GetDirectoryName(manifest.ExecutablePath) ?? manifest.InstallDirectoryPath;
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = runnerInfo.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["WINEPREFIX"] = Path.Combine(manifest.InstallDirectoryPath, "wineprefix"),
                ["WINEDEBUG"] = "-all"
            }
        };

        processStartInfo.ArgumentList.Add(manifest.ExecutablePath);

        return processStartInfo;
    }

    private static List<string> FindDosboxConfigFiles(InstalledGameManifest manifest)
    {
        var configFiles = new List<string>();

        // Checking if we have as arguments:
        if (!string.IsNullOrWhiteSpace(manifest.Arguments) && manifest.Arguments.Contains("-conf"))
        {
            var matches = DosBoxConfigFiles().Matches(manifest.Arguments);

            foreach (Match match in matches)
            {
                var rawPath = match.Groups[1].Value.Replace('\\', '/');
                var fileName = Path.GetFileName(rawPath);

                var candidatePaths = new[] 
                {
                    !string.IsNullOrWhiteSpace(manifest.WorkingDirectory) ? Path.Combine(manifest.WorkingDirectory, fileName) : null,
                    Path.Combine(manifest.InstallDirectoryPath, fileName),
                    Path.GetFullPath(Path.Combine(manifest.InstallDirectoryPath, rawPath))
                };

                var resolvedPath = candidatePaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .FirstOrDefault(File.Exists);

                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    configFiles.Add(resolvedPath);
                }
                else
                {
                    var locatedFile = Directory.EnumerateFiles(manifest.InstallDirectoryPath, fileName,
                        SearchOption.AllDirectories).FirstOrDefault();

                    if (locatedFile is not null)
                    {
                        configFiles.Add(locatedFile);
                    }
                }
            }    
        }
        else
        {
            // No arguments, so searching in the root folder:
            var locatedFiles = Directory.EnumerateFiles(manifest.InstallDirectoryPath, "dosbox*.conf");
            configFiles.AddRange(locatedFiles);
        }
        

        return configFiles;
    }
    
    // Properties:
    public static LauncherService Instance { get; } = new();
    
    // Some Regex pre-compiled patterns:
    [GeneratedRegex("""-conf\s+"?([^"\s]+)"?""")] private static partial Regex DosBoxConfigFiles();
}