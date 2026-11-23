using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using TreasureChest.Source.Services.Launcher.LaunchConfigs;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Install;

public partial class LaunchConfigurationService
{
    // Private Members:
    private readonly JsonSerializerOptions _serializerOptions;
    
    private LaunchConfigurationService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }
    
    public void CreateAndSaveLaunchConfiguration(InstalledGameManifest installManifest)
    {
        var targetFileName = $"mainLaunchParameters_{installManifest.PrimaryTask.RunnerType.ToString().ToLowerInvariant()}.json";
        var targetFilePath = Path.Combine(installManifest.InstallDirectoryPath, targetFileName);

        var jsonText = string.Empty;
        
        switch (installManifest.PrimaryTask.RunnerType)
        {
            case ERunnerType.Native:
                var nativeLaunchConfig = new NativeLaunchConfiguration
                {
                    RunnerType = ERunnerType.Native,
                    ExecutablePath = installManifest.PrimaryTask.Path,
                    WorkingDirectory = installManifest.PrimaryTask.WorkingDirectory ?? installManifest.InstallDirectoryPath,
                    CustomArguments = installManifest.PrimaryTask.Arguments,
                    RunInTerminal = false
                };
                
                jsonText = JsonSerializer.Serialize(nativeLaunchConfig, _serializerOptions);
                break;
            case ERunnerType.DosBox:
                var (mainConfigurationFilePath, customConfigurationFilePath) = FindDosBoxConfigurationFiles(
                    installManifest.InstallDirectoryPath, installManifest.PrimaryTask.WorkingDirectory, installManifest.PrimaryTask.Arguments);
                
                var dosBoxLaunchConfig = new DosBoxLaunchConfiguration
                {
                    RunnerType = ERunnerType.DosBox,
                    ExecutablePath = installManifest.PrimaryTask.Path,
                    WorkingDirectory = installManifest.PrimaryTask.WorkingDirectory ?? installManifest.InstallDirectoryPath,
                    CustomArguments = installManifest.PrimaryTask.Arguments,
                    MainConfPath = mainConfigurationFilePath,
                    CustomConfPath = customConfigurationFilePath
                };
                
                jsonText = JsonSerializer.Serialize(dosBoxLaunchConfig, _serializerOptions);
                break;
            case ERunnerType.ScummVm:
                var gameTargetId = ParseScummVmArguments(installManifest.InstallDirectoryPath, installManifest.PrimaryTask.Arguments);
                
                var scummVmLaunchConfig = new ScummVmLaunchConfiguration
                {
                    RunnerType = ERunnerType.ScummVm,
                    ExecutablePath = installManifest.PrimaryTask.Path,
                    WorkingDirectory = installManifest.InstallDirectoryPath,
                    CustomArguments = installManifest.PrimaryTask.Arguments,
                    GameTargetId = gameTargetId,
                    SaveDirectoryPath = Path.Combine(installManifest.InstallDirectoryPath, "saves"),
                };
                
                jsonText = JsonSerializer.Serialize(scummVmLaunchConfig, _serializerOptions);
                break;
            case ERunnerType.Wine:
                var wineLaunchConfig = new WineLaunchConfiguration
                {
                    RunnerType = ERunnerType.Wine,
                    ExecutablePath = Path.Combine(installManifest.InstallDirectoryPath, installManifest.PrimaryTask.Path),
                    WorkingDirectory = !string.IsNullOrWhiteSpace(installManifest.PrimaryTask.WorkingDirectory)
                        ? Path.Combine(installManifest.InstallDirectoryPath, installManifest.PrimaryTask.WorkingDirectory)
                        : installManifest.InstallDirectoryPath,
                    CustomArguments = installManifest.PrimaryTask.Arguments,
                    PrefixPath = Path.Combine(installManifest.InstallDirectoryPath, "pfx")
                };

                jsonText = JsonSerializer.Serialize(wineLaunchConfig, _serializerOptions);
                break;
            case ERunnerType.Proton:
                
                break;
            case ERunnerType.InnoExtract:
                
                break;
            case ERunnerType.Unknown:
                
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!string.IsNullOrWhiteSpace(jsonText))
        {
            File.WriteAllText(targetFilePath, jsonText);
        }
    }
    
    public bool TryGetPrimaryTaskConfig(string gameSlug, [NotNullWhen(true)] out LaunchConfiguration? launchConfiguration)
    {
        launchConfiguration = null;

        if (!InstallerService.Instance.TryGetInstallManifest(gameSlug, out var installManifest) 
            || !Directory.Exists(installManifest.InstallDirectoryPath))
            return false;
        
        var configFilePath = Directory
            .EnumerateFiles(installManifest.InstallDirectoryPath, "mainLaunchParameters_*.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(configFilePath))
            return false;

        try
        {
            var jsonText = File.ReadAllText(configFilePath);
            var fileName = Path.GetFileNameWithoutExtension(configFilePath);
            var runnerSuffixStr = fileName.Split('_').LastOrDefault()?.ToLowerInvariant();

            launchConfiguration = runnerSuffixStr switch
            {
                "dosbox" => JsonSerializer.Deserialize<DosBoxLaunchConfiguration>(jsonText, _serializerOptions),
                "native" => JsonSerializer.Deserialize<NativeLaunchConfiguration>(jsonText, _serializerOptions),
        
                "wine" => JsonSerializer.Deserialize<WineLaunchConfiguration>(jsonText, _serializerOptions),
                "scummvm" => JsonSerializer.Deserialize<ScummVmLaunchConfiguration>(jsonText, _serializerOptions),
                _ => null
            };

            return launchConfiguration is not null;
        }
        catch (Exception e)
        {
            Console.Out.WriteLine($"[WARN] - Failed to deserialize launch config '{configFilePath}': {e.Message}");
            return false;
        }
    }
    
    private (string MainConfigurationFilePath, string CustomConfigurationFilePath) FindDosBoxConfigurationFiles(
        string installDirectoryPath, string? workingDirectoryPath, string customArguments)
    {
        // First, we try to find the files from the arguments list, if any:
        var configFiles = new List<string>();

        // Checking if we have as arguments:
        if (!string.IsNullOrWhiteSpace(installDirectoryPath) && 
            !string.IsNullOrWhiteSpace(customArguments) && 
            customArguments.Contains("-conf"))
        {
            var matches = DosBoxConfigFiles().Matches(customArguments);

            foreach (Match match in matches)
            {
                var rawPath = match.Groups[1].Value.Replace('\\', '/');
                var fileName = Path.GetFileName(rawPath);

                var candidatePaths = new[] 
                {
                    !string.IsNullOrWhiteSpace(workingDirectoryPath) ? Path.Combine(workingDirectoryPath, fileName) : null,
                    Path.Combine(installDirectoryPath, fileName),
                    Path.GetFullPath(Path.Combine(installDirectoryPath, rawPath))
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
                    var locatedFile = Directory.EnumerateFiles(installDirectoryPath, fileName,
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
            configFiles.AddRange(Directory.EnumerateFiles(installDirectoryPath, "dosbox*.conf"));
        }
        
        var mainConfig = configFiles.FirstOrDefault(f => !f.EndsWith("single.conf", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var customConfig = configFiles.FirstOrDefault(f => f.EndsWith("single.conf", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        return (mainConfig, customConfig);
    }

    private static string ParseScummVmArguments(string installFolder, string arguments)
    {
        var initFile = string.Empty;
        var gameTargetId = string.Empty;
        
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            // Parsing the arguments list:
            var tokens = arguments
                .Replace("\\", "/")
                .Replace("\"", string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var rawIni = tokens.FirstOrDefault(a => a.EndsWith(".ini", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(rawIni))
            {
                initFile = Path.Combine(installFolder, Path.GetFileName(rawIni));
            }
            
            gameTargetId = tokens.LastOrDefault(t => 
                               !t.StartsWith('-') &&
                               !t.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(initFile))
        {
            // Trying to find automatically a .ini file in the installation folder:
            initFile = Directory.EnumerateFiles(installFolder, "*.ini", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(gameTargetId) && File.Exists(initFile))
        {
            var sectionLine = File.ReadAllLines(initFile)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith('[') && l.EndsWith(']') && !l.Equals("[scummvm]", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(sectionLine))
            {
                gameTargetId = sectionLine[1..^1];
            }
        }
        
        return gameTargetId;
    }
    
    // Properties:
    public static LaunchConfigurationService Instance { get; } = new();
    
    // Some Regex pre-compiled patterns:
    [GeneratedRegex("""-conf\s+"?([^"\s]+)"?""")] private static partial Regex DosBoxConfigFiles();
}