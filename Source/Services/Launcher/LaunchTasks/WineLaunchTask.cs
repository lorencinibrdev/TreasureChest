using System.Diagnostics;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchTasks;

public class WineLaunchTask : ILaunchTask
{
    private WineLaunchTask(WineLaunchConfiguration launchConfig, RunnerInfo runnerInfo)
    {
        LaunchConfiguration = launchConfig;
        ProcessStartInfo = GenerateProcessInfo(runnerInfo);
    }

    public static async Task<WineLaunchTask?> TryCreate(WineLaunchConfiguration launchConfig)
    {
        if (string.IsNullOrWhiteSpace(launchConfig.ExecutablePath) || !File.Exists(launchConfig.ExecutablePath))
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Game executable not found at {launchConfig.ExecutablePath}");
            return null;
        }

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
        
        return new WineLaunchTask(launchConfig, runnerInfo);
    }
    
    private ProcessStartInfo GenerateProcessInfo(RunnerInfo runnerInfo)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = runnerInfo.ExecutablePath,
            WorkingDirectory = LaunchConfiguration.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        
        if (!string.IsNullOrWhiteSpace(runnerInfo.LibraryPathOverride))
        {
            startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = runnerInfo.LibraryPathOverride;
        }

        if (!string.IsNullOrWhiteSpace(LaunchConfiguration.PrefixPath))
        {
            var parentDir = Path.GetDirectoryName(LaunchConfiguration.PrefixPath);

            if (!string.IsNullOrWhiteSpace(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            
            startInfo.EnvironmentVariables["WINEPREFIX"] = LaunchConfiguration.PrefixPath;
        }

        startInfo.EnvironmentVariables["WINEESYNC"] = LaunchConfiguration.EnableEsync ? "1" : "0";
        startInfo.EnvironmentVariables["WINEFSYNC"] = LaunchConfiguration.EnableFsync ? "1" : "0";
        startInfo.EnvironmentVariables["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d";
        startInfo.EnvironmentVariables["WINEDEBUG"] = "fixme-all";
        
        startInfo.ArgumentList.Add(LaunchConfiguration.ExecutablePath);

        if (!string.IsNullOrWhiteSpace(LaunchConfiguration.CustomArguments))
        {
            foreach (var arg in SplitArguments(LaunchConfiguration.CustomArguments))
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        return startInfo;
    }
    
    private static IEnumerable<string> SplitArguments(string rawArgs)
    {
        // Handles quoted arguments like: -config "my config.ini" -fullscreen
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in rawArgs)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
    
    // Properties:
    private WineLaunchConfiguration LaunchConfiguration { get; }
    public ProcessStartInfo ProcessStartInfo { get; }
}