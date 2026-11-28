using System.Collections.Concurrent;
using System.Diagnostics;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Launcher.LaunchTasks;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher;

public class LauncherService
{
    // Private Members:
    private readonly ConcurrentDictionary<long, string> _lastLogPaths = [];
    
    private LauncherService()
    {
        
    }

    public async Task<int> LaunchGameAsync(GameEntry gameEntry, CancellationToken cancellationToken, Func<ERunnerType, Task<bool>>? onMissingRunner = null)
    {
        if (!LaunchConfigurationService.Instance.TryGetPrimaryTaskConfig(gameEntry.Slug, out var launchConfiguration))
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to fetch the Primary Task for the requested game: {gameEntry.Slug}", cancellationToken);
            return -1;
        }

        var runnerInformation = RunnerManagerService.GetRunnerInformation(launchConfiguration.RunnerType);

        if (string.IsNullOrWhiteSpace(runnerInformation.ExecutablePath) && runnerInformation.Type != ERunnerType.Native)
        {
            if (onMissingRunner is null || !await onMissingRunner(launchConfiguration.RunnerType))
            {
                await Console.Out.WriteLineAsync($"$[ERROR] - Launch aborted: Missing runner type '{launchConfiguration.RunnerType}' for '{gameEntry.Slug}'", cancellationToken);
                return -1;
            }
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
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to generate the Launch Task for the game: {gameEntry.Slug}", cancellationToken);
            return -1;
        }
        
        // Preparing the temporary log stream/file:
        var logPath = Path.Combine(Path.GetTempPath(), $"treasurechest_{gameEntry.Slug}.log");
        await using var fileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var logWriter = TextWriter.Synchronized(new StreamWriter(fileStream) { AutoFlush =  true });
        
        await logWriter.WriteLineAsync("=== Treasure Chest Execution Log ===", cancellationToken);
        await logWriter.WriteLineAsync($"Game: {gameEntry.Title} ({gameEntry.Slug})", cancellationToken);
        await logWriter.WriteLineAsync($"Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}", cancellationToken);
        await logWriter.WriteLineAsync($"Command: {launchTask.ProcessStartInfo.FileName} {launchTask.ProcessStartInfo.Arguments}", cancellationToken);
        await logWriter.WriteLineAsync($"WorkingDir: {launchTask.ProcessStartInfo.WorkingDirectory}", cancellationToken);
        await logWriter.WriteLineAsync(new string('=', 40), cancellationToken);
        await logWriter.WriteLineAsync(cancellationToken);
        
        _lastLogPaths[gameEntry.Id] = logPath;

        var binaryName = Path.GetFileName(launchTask.ProcessStartInfo.FileName);
        var startedAt = DateTimeOffset.UtcNow;
        var stopWatch = Stopwatch.StartNew();
        var exitCode = -1;

        LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Running);

        using var process = new Process();
        process.StartInfo = launchTask.ProcessStartInfo;

        process.OutputDataReceived += (_, args) => HandleProcessOutput(logWriter, binaryName, args.Data);
        process.ErrorDataReceived += (_, args) => HandleProcessOutput(logWriter, binaryName, args.Data);
        
        try
        {
            if (!process.Start())
            {
                await logWriter.WriteLineAsync($"[ERROR] - Failed to launch {launchTask.ProcessStartInfo.FileName}", cancellationToken);
                return -1;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            exitCode = process.ExitCode;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to launch game: {e.Message}", cancellationToken);
            process.Kill(entireProcessTree: true);
            exitCode = -1;
        }
        finally
        {
            stopWatch.Stop();
            await logWriter.WriteLineAsync(cancellationToken);
            await logWriter.WriteLineAsync($@"=== Exited with code {exitCode} after {stopWatch.Elapsed:hh\:mm\:ss} ===", cancellationToken);
            
            LibraryService.Instance.SetGameState(gameEntry.Id, EGameState.Installed);
            _ = LibraryService.Instance.RecordPlaySessionAsync(gameEntry.Id, stopWatch.Elapsed, startedAt);
        }
        
        return exitCode;
    }

    public bool TryGetLastLogPath(long gameId, out string logPath)
    {
        if (_lastLogPaths.TryGetValue(gameId, out var path) && File.Exists(path))
        {
            logPath = path;
            return true;
        }

        logPath = string.Empty;
        return false;
    }
    
    private static void HandleProcessOutput(TextWriter logWriter, string binaryName, string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return;

        var line = $"[{DateTime.Now:HH:mm:ss}] {data}";
        Console.Out.WriteLine($"[GAME_LOG] ({binaryName}): {line}");
        logWriter.WriteLine(line);
    }
    
    // Properties:
    public static LauncherService Instance { get; } = new();
}