using System.Diagnostics;

namespace TreasureChest.Source.Services.Launcher.LaunchTasks;

public interface ILaunchTask
{
    // Public Properties:
    public ProcessStartInfo ProcessStartInfo { get; }
}