using System.Diagnostics;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;

namespace TreasureChest.Source.Services.Launcher.LaunchTasks;

public class NativeLaunchTask : ILaunchTask
{
    private NativeLaunchTask(NativeLaunchConfiguration launchConfig)
    {
        LaunchConfiguration = launchConfig;
        ProcessStartInfo = GenerateProcessInfo();
    }

    public static NativeLaunchTask? TryCreate(NativeLaunchConfiguration launchConfig)
    {
        if (string.IsNullOrWhiteSpace(launchConfig.ExecutablePath))
            return null;
        
        var launchTask = new NativeLaunchTask(launchConfig);
        return launchTask;
    }
    
    private ProcessStartInfo GenerateProcessInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = LaunchConfiguration.ExecutablePath,
            WorkingDirectory = LaunchConfiguration.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        return startInfo;
    }
    
    // Properties:
    private NativeLaunchConfiguration LaunchConfiguration { get; }
    public ProcessStartInfo ProcessStartInfo { get; }
}