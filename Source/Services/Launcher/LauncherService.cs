using System.Diagnostics;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher;

public class LauncherService
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
                // processStartInfo = CreateNativeProcessStartInfo(manifest.Value);
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
            WorkingDirectory = manifest.InstallDirectoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        
        // Finding the GOG's config files:
        var configFiles = Directory.GetFiles(manifest.InstallDirectoryPath, "*.conf")
            .Where(f => Path.GetFileName(f).StartsWith("dosbox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Length)
            .ToList();

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
    
    // Properties:
    public static LauncherService Instance { get; } = new();
}