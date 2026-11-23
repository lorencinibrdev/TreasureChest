using System.Diagnostics;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Launcher.LaunchTasks;

public class DosBoxLaunchTask : ILaunchTask
{
    private DosBoxLaunchTask(DosBoxLaunchConfiguration launchConfig, RunnerInfo runnerInfo)
    {
        LaunchConfiguration = launchConfig;
        ProcessStartInfo = GenerateProcessInfo(runnerInfo);
    }

    public static DosBoxLaunchTask? TryCreate(DosBoxLaunchConfiguration launchConfig)
    {
        // Trying to fetch the runner information:
        var runnerInfo = RunnerManagerService.GetRunnerInformation(launchConfig.RunnerType);

        if (!runnerInfo.IsAvailable)
            return null;

        var launchTask = new DosBoxLaunchTask(launchConfig, runnerInfo);
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
        
        if (!string.IsNullOrWhiteSpace(LaunchConfiguration.MainConfPath))
        {
            startInfo.ArgumentList.Add("-conf");
            startInfo.ArgumentList.Add(LaunchConfiguration.MainConfPath);
        }
        
        if (!string.IsNullOrWhiteSpace(LaunchConfiguration.CustomConfPath))
        {
            startInfo.ArgumentList.Add("-conf");
            startInfo.ArgumentList.Add(LaunchConfiguration.CustomConfPath);
        }

        if (LaunchConfiguration.ForceFullscreen)
        {
            startInfo.ArgumentList.Add("-fullscreen");
        }

        return startInfo;
    }
    
    // Properties:
    private DosBoxLaunchConfiguration LaunchConfiguration { get; }
    public ProcessStartInfo ProcessStartInfo { get; }
}