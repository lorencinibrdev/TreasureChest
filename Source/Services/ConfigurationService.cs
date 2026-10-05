using System.Text.Json;

namespace TreasureChest.Source.Services;

public abstract class ConfigurationService
{
    public static ApplicationConfiguration LoadConfiguration()
    {
        // If the config file does not exist, we are on the first run:
        if (!File.Exists(ConfigurationFilePath))
        {
            var newConfiguration = GenerateDefaultConfiguration();
            return newConfiguration;
        }
        
        // Else, we are running from an already created configuration:
        try
        {
            var jsonText = File.ReadAllText(ConfigurationFilePath);
            var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(jsonText);
            return configuration ?? GenerateDefaultConfiguration();
        }
        catch
        {
            return GenerateDefaultConfiguration();
        }
    }

    private static ApplicationConfiguration GenerateDefaultConfiguration()
    {
        return new ApplicationConfiguration
        {
            IsFirstRun = true
        };
    }

    public static void SaveConfiguration(ApplicationConfiguration configuration)
    {
        configuration.IsFirstRun = false;
        Directory.CreateDirectory(ConfigurationsDirectory);
        var jsonText = JsonSerializer.Serialize(configuration, SerializerOptions);
        File.WriteAllText(ConfigurationFilePath, jsonText);
    }
    
    // Properties:
    public static string ApplicationId { get; } = "io.github.lorencinibrdev.TreasureChest";
    public static string ConfigurationsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationId);
    private static string ConfigurationFileName { get; } = "config.json";
    private static string ConfigurationFilePath { get; } = Path.Combine(ConfigurationsDirectory, ConfigurationFileName);

    private static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        WriteIndented = true
    };
}