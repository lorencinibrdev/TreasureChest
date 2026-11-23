using System.Diagnostics;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchTasks;

public class ScummVmLaunchTask : ILaunchTask
{
    private ScummVmLaunchTask(ScummVmLaunchConfiguration launchConfig, RunnerInfo runnerInfo)
    {
        LaunchConfiguration = launchConfig;
        ProcessStartInfo = GenerateProcessInfo(runnerInfo);
    }

    public static ScummVmLaunchTask? TryCreate(ScummVmLaunchConfiguration launchConfig)
    {
        // Trying to fetch the runner information:
        var runnerInfo = RunnerManagerService.GetRunnerInformation(launchConfig.RunnerType);

        if (!runnerInfo.IsAvailable)
            return null;

        var launchTask = new ScummVmLaunchTask(launchConfig, runnerInfo);
        return launchTask;
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
        
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(LaunchConfiguration.WorkingDirectory);

        if (!string.IsNullOrWhiteSpace(LaunchConfiguration.SaveDirectoryPath))
        {
            Directory.CreateDirectory(LaunchConfiguration.SaveDirectoryPath);
            startInfo.ArgumentList.Add($"--savepath={LaunchConfiguration.SaveDirectoryPath}");
        }

        if (LaunchConfiguration.ForceFullscreen)
        {
            startInfo.ArgumentList.Add("--fullscreen");
        }

        startInfo.ArgumentList.Add(!string.IsNullOrWhiteSpace(LaunchConfiguration.GameTargetId)
            ? LaunchConfiguration.GameTargetId
            : "--auto-detect");

        return startInfo;
    }
    
    // Properties:
    private ScummVmLaunchConfiguration LaunchConfiguration { get; }
    public ProcessStartInfo ProcessStartInfo { get; }
}