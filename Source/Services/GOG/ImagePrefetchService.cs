using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Services.GOG;

public class CachedGameImages
{
    // Properties:
    public string LogoPath { get; set; } = string.Empty;
    public string Logo2XPath { get; set; } = string.Empty;
    public string SidebarIconPath { get; set; } = string.Empty;
    public string SidebarIcon2XPath { get; set; } = string.Empty;
    public string BackgroundPath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string MenuNotificationAvPath { get; set; } = string.Empty;
    public string MenuNotificationAv2Path { get; set; } = string.Empty;
}

public class ImagePrefetchService
{
    // Private Members:
    private readonly HttpClient _imageDownloadClient = new();
    private readonly SemaphoreSlim _throttleTasker = new(10);
    private readonly ConcurrentDictionary<string, Task<string>> _ongoingTasks = new();
    private readonly ConcurrentDictionary<long, CachedGameImages> _cachedPaths = new();

    private ImagePrefetchService()
    {
        
    }

    public async Task<string> FetchImageWithPriorityAsync(long gameId, EImageType imageType, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        
        var localPath = BuildLocalPath(gameId.ToString(), imageType, url);

        if (File.Exists(localPath))
        {
            UpdateCacheStateForGame(gameId, imageType, localPath);
            return localPath;
        }

        return await _ongoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(gameId, imageType, url, localPath, true));
    }
    
    public async Task PrefetchLibraryImagesAsync(IReadOnlyList<GameEntry> gameList)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Starting background prefetch for {gameList.Count} games.");
        
        var tasks = new List<Task>();
        
        foreach (var game in gameList)
        {
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.Logo2X, game.ImageLinks.Logo2X);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.SidebarIcon2X, game.ImageLinks.SidebarIcon2X);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.Logo, game.ImageLinks.Logo);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.SidebarIcon, game.ImageLinks.SidebarIcon);
        }

        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Essential UI assets (Covers & Icons) prefetch complete.");
        
        tasks.Clear();
        
        foreach (var game in gameList)
        {
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.Background, game.ImageLinks.Background);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.Icon, game.ImageLinks.Icon);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.MenuNotificationAv, game.ImageLinks.MenuNotificationAv);
            EnqueueBackgroundDownload(tasks, game.Id, EImageType.MenuNotificationAv2, game.ImageLinks.MenuNotificationAv2);
        }
        
        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Secondary media prefetch complete.");
        await Console.Out.WriteLineAsync($"Loaded {gameList.Count} images. Prefetch completed.");
    }

    public bool TryGetCachedImagePath(long gameId, EImageType imageType, [NotNullWhen(true)]out string? localPath)
    {
        if (_cachedPaths.TryGetValue(gameId, out var cachedGameImages))
        {
            var foundPath = imageType switch
            {
                EImageType.Background => cachedGameImages.BackgroundPath,
                EImageType.Logo => cachedGameImages.LogoPath,
                EImageType.Logo2X => cachedGameImages.Logo2XPath,
                EImageType.SidebarIcon => cachedGameImages.SidebarIconPath,
                EImageType.SidebarIcon2X => cachedGameImages.SidebarIcon2XPath,
                EImageType.MenuNotificationAv => cachedGameImages.MenuNotificationAvPath,
                EImageType.MenuNotificationAv2 => cachedGameImages.MenuNotificationAv2Path,
                EImageType.Icon => cachedGameImages.IconPath,
                _ => throw new ArgumentOutOfRangeException(nameof(imageType), imageType, null)
            };

            if (!string.IsNullOrWhiteSpace(foundPath))
            {
                localPath = foundPath;
                return true;
            }
        }

        localPath = null;
        return false;
    }
    
    private async Task<string> DownloadImageInternalAsync(long gameId, EImageType imageType, string url, string localPath, bool isPriority)
    {
        if (url.StartsWith("//"))
        {
            url = "https:" + url;
        }

        if (!isPriority)
        {
            await _throttleTasker.WaitAsync();
        }
        
        try
        {
            var tempPath = $"{localPath}.tmp";
            var imageData = await _imageDownloadClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, imageData);
            File.Move(tempPath, localPath, true);

            UpdateCacheStateForGame(gameId, imageType, localPath);
            
            return localPath;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARN] - Failed to download cover image for {localPath}: {e.Message}");
            return string.Empty;
        }
        finally
        {
            if (!isPriority)
            {
                _throttleTasker.Release();    
            }
            
            _ongoingTasks.TryRemove(localPath, out _);
        }
    }

    private void UpdateCacheStateForGame(long gameId, EImageType imageType, string localPath)
    {
        var cachedData = _cachedPaths.GetOrAdd(gameId, _ => new CachedGameImages());

        lock (cachedData)
        {
            switch (imageType)
            {
                case EImageType.Background:
                    cachedData.BackgroundPath = localPath;
                    break;
                case EImageType.Logo:
                    cachedData.LogoPath = localPath;
                    break;
                case EImageType.Logo2X:
                    cachedData.Logo2XPath = localPath;
                    break;
                case EImageType.SidebarIcon:
                    cachedData.SidebarIconPath = localPath;
                    break;
                case EImageType.SidebarIcon2X:
                    cachedData.SidebarIcon2XPath = localPath;
                    break;
                case EImageType.MenuNotificationAv:
                    cachedData.MenuNotificationAvPath = localPath;
                    break;
                case EImageType.MenuNotificationAv2:
                    cachedData.MenuNotificationAv2Path = localPath;
                    break;
                case EImageType.Icon:
                    cachedData.IconPath = localPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(imageType), imageType, null);
            }
        }
        
        _cachedPaths[gameId] = cachedData;
    }

    private void EnqueueBackgroundDownload(List<Task> tasks, long gameId, EImageType imageType, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var localPath = BuildLocalPath(gameId.ToString(), imageType, url);

        if (File.Exists(localPath))
        {
            UpdateCacheStateForGame(gameId, imageType, localPath);
            return;
        }
        
        var downloadTask = _ongoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(gameId, imageType, url, localPath, false));
        tasks.Add(downloadTask);
    }

    private static string BuildLocalPath(string gameId, EImageType imageType, string url)
    {
        var ext = Path.GetExtension(url.Split('?')[0]);

        if (string.IsNullOrEmpty(ext))
        {
            ext = ".png";
        }

        var gameFolderPath = Path.Combine(LibraryImageCacheDirectoryPath, gameId);
        Directory.CreateDirectory(gameFolderPath);
        
        return Path.Combine(gameFolderPath, $"{gameId}_{ConvertImageType(imageType)}{ext}");
    }

    private static string ConvertImageType(EImageType type)
    {
        return type switch
        {
            EImageType.Background => "background",
            EImageType.Logo => "logo",
            EImageType.Logo2X => "logo2x",
            EImageType.SidebarIcon => "sidebarIcon",
            EImageType.SidebarIcon2X => "sidebarIcon2X",
            EImageType.MenuNotificationAv => "menuNotificationAv",
            EImageType.MenuNotificationAv2 => "menuNotificationAv2",
            EImageType.Icon => "icon",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    
    // Properties:
    public static ImagePrefetchService Instance { get; } = new();
    private static string LibraryImageCacheDirectoryPath => Path.Combine(SettingsService.CacheDirectory, "images", "library");
}