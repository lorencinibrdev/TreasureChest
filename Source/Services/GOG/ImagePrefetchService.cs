using System.Collections.Concurrent;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Services.GOG;

public class ImagePrefetchService
{
    // Private Members:
    private readonly HttpClient _imageDownloadClient = new();
    private readonly SemaphoreSlim _throttleTasker = new(10);
    private readonly ConcurrentDictionary<string, Task<string>> _ongoingTasks = new();

    private ImagePrefetchService()
    {
        
    }

    public async Task<string> FetchImageWithPriorityAsync(long gameId, EImageType imageType, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        
        var localPath = BuildLocalPath(gameId.ToString(), imageType, url);

        if (File.Exists(localPath))
            return localPath;

        return await _ongoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(url, localPath, true));
    }
    
    public async Task PrefetchLibraryImagesAsync(IReadOnlyList<GameEntry> gameList)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Starting background prefetch for {gameList.Count} games.");
        
        var tasks = new List<Task>();
        
        foreach (var game in gameList)
        {
            EnqueueBackgroundDownload(tasks, game.Id.ToString(), EImageType.Logo2X, game.ImageLinks.Logo2X);
            EnqueueBackgroundDownload(tasks, game.Id.ToString(), EImageType.SidebarIcon2X, game.ImageLinks.SidebarIcon2X);
            EnqueueBackgroundDownload(tasks, game.Id.ToString(), EImageType.Logo, game.ImageLinks.Logo);
            EnqueueBackgroundDownload(tasks, game.Id.ToString(), EImageType.SidebarIcon, game.ImageLinks.SidebarIcon);
        }

        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Essential UI assets (Covers & Icons) prefetch complete.");
        
        tasks.Clear();
        
        foreach (var game in gameList)
        {
            var idStr = game.Id.ToString();
            
            EnqueueBackgroundDownload(tasks, idStr, EImageType.Background, game.ImageLinks.Background);
            EnqueueBackgroundDownload(tasks, idStr, EImageType.Icon, game.ImageLinks.Icon);
            EnqueueBackgroundDownload(tasks, idStr, EImageType.MenuNotificationAv, game.ImageLinks.MenuNotificationAv);
            EnqueueBackgroundDownload(tasks, idStr, EImageType.MenuNotificationAv2, game.ImageLinks.MenuNotificationAv2);
        }
        
        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Secondary media prefetch complete.");
        await Console.Out.WriteLineAsync($"Loaded {gameList.Count} images. Prefetch completed.");
    }
    
    private async Task<string> DownloadImageInternalAsync(string url, string localPath, bool isPriority)
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
            var imageData = await _imageDownloadClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, imageData);
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

    private void EnqueueBackgroundDownload(List<Task> tasks, string gameId, EImageType imageType, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var localPath = BuildLocalPath(gameId, imageType, url);

        if (File.Exists(localPath))
            return;
        
        var downloadTask = _ongoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(url, localPath, false));
        tasks.Add(downloadTask);
    }

    private string BuildLocalPath(string gameId, EImageType imageType, string url)
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
    private string LibraryImageCacheDirectoryPath => Path.Combine(SettingsService.CacheDirectory, "images", "library");
}