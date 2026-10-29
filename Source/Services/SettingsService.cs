using Gio;
using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Services;

public class SettingsService
{
    // Private Members
    public static SettingsService Instance { get; } = new();
    private readonly Settings _settings;

    private SettingsService()
    {
        _settings = Settings.New(ApplicationId);
        _settings.OnChanged += HandleSettingsChanged;
    }

    private void HandleSettingsChanged(Settings sender, Settings.ChangedSignalArgs args)
    {
        Console.Out.WriteLine($"[INFO] - DConf Settings changed: {args.Key}");
    }

    private static string ResolvePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        if (!rawPath.StartsWith('~')) 
            return rawPath;
        
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, rawPath.TrimStart('~', '/'));
    }
    
    // Properties:
    public string LibraryFolder {
        get => ResolvePath(_settings.GetString("library-folder"));
        set => _settings.SetString("library-folder", value);
    }
    
    public string DownloadsFolder {
        get => ResolvePath(_settings.GetString("downloads-folder"));
        set => _settings.SetString("downloads-folder", value);
    }
    
    public string RuntimesFolder {
        get => ResolvePath(_settings.GetString("runtimes-folder"));
        set => _settings.SetString("runtimes-folder", value);
    }
    
    public bool IsFirstRun {
        get => _settings.GetBoolean("first-run");
        set => _settings.SetBoolean("first-run", value);
    }
    
    public string ViewMode {
        get => _settings.GetString("view-mode");
        set => _settings.SetString("view-mode", value);
    }
    
    public EMainWindowTabs StartupDefaultView
    {
        get
        {
            var stringValue = _settings.GetString("startup-default-view");

            return Enum.TryParse<EMainWindowTabs>(stringValue, true, out var result) 
                ? result 
                : EMainWindowTabs.Library;
        }
        set => _settings.SetString("startup-default-view", value.ToString().ToLower());
    }

    public bool ShowInstalledOnly {
        get => _settings.GetBoolean("show-installed-only");
        set => _settings.SetBoolean("show-installed-only", value);
    }
    
    public bool AutoCleanDownloads {
        get => _settings.GetBoolean("auto-clean-downloads");
        set => _settings.SetBoolean("auto-clean-downloads", value);
    }
    
    public int LastWindowWidth {
        get => _settings.GetInt("last-window-width");
        set => _settings.SetInt("last-window-width", value);
    }
    
    public int LastWindowHeight {
        get => _settings.GetInt("last-window-height");
        set => _settings.SetInt("last-window-height", value);
    }
    
    public string SavedUsername {
        get => _settings.GetString("saved-username");
        set => _settings.SetString("saved-username", value);
    }
    
    public string SavedUserId {
        get => _settings.GetString("saved-user-id");
        set => _settings.SetString("saved-user-id", value);
    }
    
    public string SavedUserEmail {
        get => _settings.GetString("saved-user-email");
        set => _settings.SetString("saved-user-email", value);
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
    public static string ApplicationId => "io.github.lorencinibrdev.TreasureChest";
    public static string CacheDirectory { get; } = SetupCacheDirectory();
    public static string DataDirectory { get; } = SetupDataDirectory();
}