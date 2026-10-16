using System.Collections.Concurrent;
using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Services;

public enum EImageType
{
    Background,
    Logo,
    Logo2X,
    SidebarIcon,
    SidebarIcon2X,
    MenuNotificationAv,
    MenuNotificationAv2,
    Icon
}

public static class ImagePrefetchService
{
    // Private MEmbers:
    private static readonly HttpClient ImageDownloadClient = new();
    private static readonly SemaphoreSlim ThrottleTasker = new(10);
    private static readonly ConcurrentDictionary<string, Task<string>> OngoingTasks = new();
    private static readonly string LibraryImageCacheDirectory = EnsureImageCacheDirectoryExists();

    public static Task<string> FetchImageWithPriorityAsync(string gameId, EImageType imageType, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(gameId))
            return Task.FromResult(string.Empty);
        
        var localPath = BuildLocalPath(gameId, imageType, url);

        if (File.Exists(localPath))
            return Task.FromResult(localPath);

        return OngoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(url, localPath, true));
    }
    
    public static async Task PrefetchLibraryImagesAsync(IReadOnlyList<GameDetailsResponse> gamesList)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Starting background prefetch for {gamesList.Count} games.");
        
        var tasks = new List<Task>();

        foreach (var gameDetails in gamesList)
        {
            if (gameDetails.Images is null || gameDetails.Id is null)
                continue;

            var id = gameDetails.Id.ToString()!;
            
            EnqueueBackgroundDownload(tasks, id, EImageType.Logo2X, gameDetails.Images.Logo2X ?? gameDetails.Images.Logo);
            EnqueueBackgroundDownload(tasks, id, EImageType.SidebarIcon2X, gameDetails.Images.SideBarIcon2X ?? gameDetails.Images.SidebarIcon);
        }

        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Essential UI assets (Covers & Icons) prefetch complete.");
        
        tasks.Clear();
        
        foreach (var gameDetails in gamesList)
        {
            if (gameDetails.Images is null || gameDetails.Id is null)
                continue;

            var id = gameDetails.Id.ToString()!;
            
            EnqueueBackgroundDownload(tasks, id, EImageType.Background, gameDetails.Images.Background);
            EnqueueBackgroundDownload(tasks, id, EImageType.Icon, gameDetails.Images.Icon);
            EnqueueBackgroundDownload(tasks, id, EImageType.MenuNotificationAv, gameDetails.Images.MenuNotificationAv);
            EnqueueBackgroundDownload(tasks, id, EImageType.MenuNotificationAv2, gameDetails.Images.MenuNotificationAv2);
        }
        
        await Task.WhenAll(tasks);
        await Console.Out.WriteLineAsync("[INFO] - Secondary media prefetch complete.");
        await Console.Out.WriteLineAsync($"Loaded {gamesList.Count} images. Prefetch completed.");
    }
    
    private static async Task<string> DownloadImageInternalAsync(string url, string localPath, bool isPriority)
    {
        if (url.StartsWith("//"))
        {
            url = "https:" + url;
        }

        if (!isPriority)
        {
            await ThrottleTasker.WaitAsync();
        }
        
        try
        {
            var imageData = await ImageDownloadClient.GetByteArrayAsync(url);
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
                ThrottleTasker.Release();    
            }
            
            OngoingTasks.TryRemove(localPath, out _);
        }
    }

    private static void EnqueueBackgroundDownload(List<Task> tasks, string gameId, EImageType imageType, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var localPath = BuildLocalPath(gameId, imageType, url);

        if (File.Exists(localPath))
            return;
        
        var downloadTask = OngoingTasks.GetOrAdd(localPath, _ => DownloadImageInternalAsync(url, localPath, false));
        tasks.Add(downloadTask);
    }
    
    private static string EnsureImageCacheDirectoryExists()
    {
        var imageCacheDir = Path.Combine(ConfigurationService.CacheDirectory, "images", "library");
        
        if (!Directory.Exists(imageCacheDir))
        {
            Directory.CreateDirectory(imageCacheDir);
        }
        
        return imageCacheDir;
    }

    private static string BuildLocalPath(string gameId, EImageType imageType, string url)
    {
        var ext = Path.GetExtension(url.Split('?')[0]);

        if (string.IsNullOrEmpty(ext))
        {
            ext = ".png";
        }

        var gameFolderPath = Path.Combine(LibraryImageCacheDirectory, gameId);
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
}