using System.Text.Json;
using TreasureChest.Source.Helpers;

namespace TreasureChest.Source.Services;

public static class ConfigurationService
{
    // Private Members
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented =  true };
    private static bool _isInitialized;
    
    public static bool Initialize()
    {
        if (File.Exists(ConfigurationFilePath))
        {
            // Loading data from file:
            LoadConfigurationFromJsonFile();
        }
        else
        {
            Console.Out.WriteLine("[INFO] - Configuration file not found, so assuming first execution of the program. Generating defaults...");
            Current = new ConfigurationData();
            SaveConfigurationToJsonFile();
        }
        
        _isInitialized = true;
        return _isInitialized;
    }

    public static void FinishExecution()
    {
        Current.IsFirstRun = false;
        SaveConfigurationToJsonFile();
    }

    private static void LoadConfigurationFromJsonFile()
    {
        var jsonText = File.ReadAllText(ConfigurationFilePath);

        try
        {
            Current = JsonSerializer.Deserialize<ConfigurationData>(jsonText, SerializerOptions) ?? throw new InvalidOperationException();
            Console.Out.WriteLine($"[INFO] - Configuration file found on '{ConfigurationFilePath}', and loaded successfully.");
        }
        catch (Exception)
        {
            Console.Out.WriteLine($"[WARN] - Configuration file found on '{ConfigurationFilePath}', but the same could not be parsed.");
            Console.Out.WriteLine("[WARN] - Falling back to default configs...");
            
            File.Delete(ConfigurationFilePath);
            Current = new ConfigurationData();
            SaveConfigurationToJsonFile();
        }
    }
    
    public static void SaveConfigurationToJsonFile()
    {
        Console.Out.Write($"[INFO] - Writing configuration data to '{ConfigurationFilePath}'...");
        
        if (!Directory.Exists(ConfigurationsDirectory))
        {
            Directory.CreateDirectory(ConfigurationsDirectory);
        }
        
        var json = JsonSerializer.Serialize(Current, SerializerOptions);
        File.WriteAllText(ConfigurationFilePath, json);
        
        Console.Out.WriteLine("\tDone!");
    }

    private static string SetupCacheDirectory()
    {
        var cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");

        if (string.IsNullOrEmpty(cacheHome))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            cacheHome = Path.Combine(userProfile, ".cache");
        }

        var appCacheDir = Path.Combine(cacheHome, ApplicationId);

        if (!Directory.Exists(appCacheDir))
        {
            Directory.CreateDirectory(appCacheDir);
        }
        
        Console.Out.WriteLine($"[INFO] - Cache directory defined as '{appCacheDir}'...");
        
        return appCacheDir;
    }
    
    private static string SetupDataDirectory()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (string.IsNullOrEmpty(dataHome))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dataHome = Path.Combine(userProfile, ".local", "share");
        }

        var appCacheDir = Path.Combine(dataHome, ApplicationId);

        if (!Directory.Exists(appCacheDir))
        {
            Directory.CreateDirectory(appCacheDir);
        }
        
        Console.Out.WriteLine($"[INFO] - Data directory defined as '{appCacheDir}'...");
        
        return appCacheDir;
    }
    
    // Properties:
    public static ConfigurationData Current { get; private set; } = new();
    public static string ApplicationId => "io.github.lorencinibrdev.TreasureChest";
    public static string CacheDirectory { get; } = SetupCacheDirectory();
    public static string DataDirectory { get; } = SetupDataDirectory();

    // Private Properties:
    private static readonly string ConfigurationsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationId);
    private const string ConfigurationFileName = "config.json";
    private static readonly string ConfigurationFilePath = Path.Combine(ConfigurationsDirectory, ConfigurationFileName);
}