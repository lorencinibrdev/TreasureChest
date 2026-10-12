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
    
    // Properties:
    public static ConfigurationData Current { get; private set; } = new();
    public static string ApplicationId => "io.github.lorencinibrdev.TreasureChest";

    // Private Properties:
    private static readonly string ConfigurationsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationId);
    private const string ConfigurationFileName = "config.json";
    private static readonly string ConfigurationFilePath = Path.Combine(ConfigurationsDirectory, ConfigurationFileName);
}