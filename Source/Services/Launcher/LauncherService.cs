using System.Diagnostics;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Launcher.LaunchTasks;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Services.Launcher;

public class LauncherService
{
    private LauncherService()
    {
        
    }

    public static async Task<int> LaunchGameAsync(GameEntry gameEntry, CancellationToken cancellationToken)
    {
        if (!LaunchConfigurationService.Instance.TryGetPrimaryTaskConfig(gameEntry.Slug, out var launchConfiguration))
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to fetch the Primary Task for the requested game: {gameEntry.Slug}");
            return -1;
        }
        
        // Creating the actual run task with he fetched configuration:
        ILaunchTask? launchTask = launchConfiguration switch
        {
            DosBoxLaunchConfiguration dosboxConfig => DosBoxLaunchTask.TryCreate(dosboxConfig),
            NativeLaunchConfiguration nativeConfig => NativeLaunchTask.TryCreate(nativeConfig),
            WineLaunchConfiguration wineConfig => await WineLaunchTask.TryCreate(wineConfig),
            ScummVmLaunchConfiguration scummVmConfig => ScummVmLaunchTask.TryCreate(scummVmConfig),
            _ => null
        };

        if (launchTask is null)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to generate the Launch Task for the game: {gameEntry.Slug}");
            return -1;
        }

        LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Running);

        using var process = new Process();
        process.StartInfo = launchTask.ProcessStartInfo;

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.Out.WriteLine($"[GAME_LOG] ({Path.GetFileName(launchTask.ProcessStartInfo.FileName)}): {args.Data}");
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.Out.WriteLine($"[GAME_LOG] ({Path.GetFileName(launchTask.ProcessStartInfo.FileName)}): {args.Data}");
            }
        };
        
        try
        {
            if (!process.Start())
            {
                await Console.Out.WriteLineAsync($"[ERROR] - Failed to launch {launchTask.ProcessStartInfo.FileName}");
                return process.ExitCode;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to launch game: {e.Message}");
            process.Kill(entireProcessTree: true);
            return -1;
        }
        finally
        {
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installed);    
        }
        
        return process.ExitCode;
    }
    
    // Properties:
    public static LauncherService Instance { get; } = new();
}