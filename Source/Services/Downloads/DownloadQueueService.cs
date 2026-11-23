using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Xml.Linq;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Services.Downloads;

public class DownloadQueueService
{
    // Private Members:
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphoreSlim = new(2, 2);
    private readonly ConcurrentBag<DownloadTask> _allTasks = [];
    private readonly ConcurrentDictionary<string, DownloadManifest> _downloadedCache = [];
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Lock _manifestWriteLock = new();
    private const string ManifestFileName = "download_manifest.json";
    private const int AllocationSizeInBytes = 81920;

    private DownloadQueueService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromHours(2)
        };
        
        // Loading the already downloaded entries in the disk:
        var downloadsDir = SettingsService.Instance.DownloadsFolder;

        if (!Directory.Exists(downloadsDir))
            return;

        var manifestFiles = Directory.EnumerateFiles(downloadsDir, ManifestFileName, SearchOption.AllDirectories);

        foreach (var manifestFilePath in manifestFiles)
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<DownloadManifest>(File.ReadAllText(manifestFilePath));

                if (manifest?.Files is null) 
                    continue;
                
                var gameDir = Path.GetDirectoryName(manifestFilePath);

                if (string.IsNullOrWhiteSpace(gameDir))
                    continue;
                
                var isComplete = manifest.Files.All(f => File.Exists(Path.Combine(gameDir, f.Name)));

                if (isComplete)
                {
                    _downloadedCache[manifest.GameSlug] = manifest;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
        
        Console.WriteLine($"[INFO] - Pre-loaded {_downloadedCache.Count} downloaded games into cache.");
    }

    public async Task<DownloadTask> EnqueueDownload(long gameId, string title, string slug, ESystemPlatform platform, string? language, string url, string targetFileName)
    {
        var targetDir = Path.Join(SettingsService.Instance.DownloadsFolder, slug);

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var task = new DownloadTask
        {
            GameId = gameId,
            GameSlug = slug,
            Title = title,
            DownloadUrl = url,
            TargetFileName = targetFileName,
            DestinationFolderPath = targetDir,
            Platform = platform,
            Language = language
        };
        
        _allTasks.Add(task);
        
        // Trying to fetch the md5 for later verification:
        task.Md5Sum = await FetchMd5Hash(url, task.Cts.Token);

        if (task.Cts.IsCancellationRequested)
        {
            HandleTaskCancellation(task);
            return task;
        }
        
        NotifyUi(() => TaskAdded?.Invoke(task));
        _ = ProcessTaskAsync(task);
        return task;
    }

    public bool IsDownloadingGame(long gameId)
    {
        return _allTasks.Any(t =>
            t.GameId == gameId && t.Status 
                is EDownloadStatus.Queued
                or EDownloadStatus.Downloading 
                or EDownloadStatus.Validating);
    }

    public List<DownloadTask> GetTasksForGame(long gameId)
    {
        return
        [
            .. _allTasks.Where(t =>
                t.GameId == gameId &&
                t.Status is EDownloadStatus.Queued or EDownloadStatus.Downloading or EDownloadStatus.Validating)
        ];
    }

    private async Task ProcessTaskAsync(DownloadTask task)
    {
        var semaphoreAcquired = false;
        
        try
        {
            await _semaphoreSlim.WaitAsync(task.Cts.Token);
            semaphoreAcquired = true;
            
            task.Status = EDownloadStatus.Downloading;
            NotifyUi(() => TaskUpdated?.Invoke(task));

            using var response = await _httpClient.GetAsync(task.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
                task.Cts.Token);
            response.EnsureSuccessStatusCode();
            task.TotalBytes = response.Content.Headers.ContentLength ?? 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(task.Cts.Token);
                
            {
                await using var fileStream = new FileStream(
                    task.PartialFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: AllocationSizeInBytes,
                    useAsync: true);

                var lastReportTime = DateTime.UtcNow;
                long lastReportBytes = 0;
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(AllocationSizeInBytes);

                try
                {
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), task.Cts.Token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), task.Cts.Token);
                        task.TotalBytesDownloaded += bytesRead;

                        var now = DateTime.UtcNow;
                        var timeDiff = (now - lastReportTime).TotalSeconds;

                        if (!(timeDiff >= 0.25))
                            continue;

                        task.SpeedBytesPerSecond = (task.TotalBytesDownloaded - lastReportBytes) / timeDiff;
                        lastReportTime = now;
                        lastReportBytes = task.TotalBytesDownloaded;
                        NotifyUi(() => TaskUpdated?.Invoke(task));
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }   // File stream release

            // Validating the MD5 hash for the newly downloaded file:
            if (!string.IsNullOrWhiteSpace(task.Md5Sum))
            {
                task.Status = EDownloadStatus.Validating;
                NotifyUi(() => TaskUpdated?.Invoke(task));

                var isValid = await VerifyFileMd5Async(task.PartialFilePath, task.Md5Sum, task.Cts.Token);

                if (!isValid)
                {
                    await Console.Out.WriteLineAsync($"[ERROR] - MD5 Mismatch for {task.Title}");
                    task.Status = EDownloadStatus.Failed;
                    CleanUpPartialFile(task);
                    NotifyUi(() => TaskUpdated?.Invoke(task));
                    return;
                }
            }

            File.Move(task.PartialFilePath, task.FinalFilePath, true);

            task.Status = EDownloadStatus.Completed;
            
            // Writing the JSON manifest file in the downloaded folder, for later checking:
            CreateOrUpdateDownloadManifest(task);
            
            NotifyUi(() =>
            {
                TaskUpdated?.Invoke(task);
                TaskCompleted?.Invoke(task);
            });
        }
        catch (OperationCanceledException)
        {
            HandleTaskCancellation(task);
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _semaphoreSlim.Release();    
            }
        }
    }
    
    public static void CancelDownload(long gameId, string gameSlug)
    {
        var tasks = Instance.GetTasksForGame(gameId);

        if (tasks.Count == 0)
            return;
                    
        Console.Out.WriteLine($"[INFO] - Cancelling the download of {tasks.Count} files, for '{gameSlug}'");

        foreach (var downloadTask in tasks)
        {
            if (downloadTask.Status is not (EDownloadStatus.Downloading or EDownloadStatus.Queued or EDownloadStatus.Validating)) 
                continue;
            
            downloadTask.Cts.Cancel();
        }
    }

    public bool TryGetDownloadManifest(string gameSlug, [NotNullWhen(true)] out DownloadManifest? downloadManifest)
    {
        if (_downloadedCache.TryGetValue(gameSlug, out var actualValue))
        {
            downloadManifest = actualValue;
            return true;
        }

        downloadManifest = null;
        return false;
    }

    public bool IsGameDownloaded(string gameSlug)
    {
        // Cache First:
        if (!_downloadedCache.TryGetValue(gameSlug, out _))
            return false;
        
        var downloadedFolder = Path.Join(SettingsService.Instance.DownloadsFolder, gameSlug);
        var manifestFilePath = Path.Join(downloadedFolder, ManifestFileName);

        if (!File.Exists(manifestFilePath))
            return false;

        try
        {
            var manifest = JsonSerializer.Deserialize<DownloadManifest>(File.ReadAllText(manifestFilePath));

            return manifest?.Files is not null &&
                   manifest.Files.All(f => File.Exists(Path.Combine(downloadedFolder, f.Name)));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void ClearDownloadedFiles(GameEntry gameEntry)
    {
        var gameId = gameEntry.Id;
        var gameSlug = gameEntry.Slug;

        var downloadDir = TryGetDownloadManifest(gameSlug, out var downloadManifest)
            ? downloadManifest.DownloadDirectoryPath
            : Path.Join(SettingsService.Instance.DownloadsFolder, gameSlug);
        
        try
        {
            if (!Directory.Exists(downloadDir)) 
                return;
            
            Console.Out.WriteLine($"[INFO] - Cleaning up data on path '{downloadDir}'");
            Directory.Delete(downloadDir, true);
            Console.Out.WriteLine($"[INFO] - Done cleaning data for {gameSlug}");
        }
        catch (Exception e)
        {
            Console.Out.WriteLine($"[WARN] - Failed to recursively remove the download folder: {downloadDir}");
            Console.Out.WriteLine($"[WARN] - {e.Message}");
        }
        finally
        {
            _downloadedCache.TryRemove(gameSlug, out _);
            LibraryService.Instance.SetGameState(gameId, EGameState.Available);
        }
    }

    private static void NotifyUi(Action action)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            action();
            return false;
        });
    }
    
    private static void CleanUpPartialFile(DownloadTask task)
    {
        try
        {
            if (File.Exists(task.PartialFilePath))
            {
                File.Delete(task.PartialFilePath);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    private static async Task<bool> VerifyFileMd5Async(string filePath, string expectedMd5, CancellationToken token)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Checking MD5 hash for file: '{filePath}'", token);
        using var md5 = System.Security.Cryptography.MD5.Create();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true
        );
        
        var hashBytes = await md5.ComputeHashAsync(stream, token);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        await Console.Out.WriteLineAsync($"[INFO] - Expected: '{expectedMd5}'", token);
        await Console.Out.WriteLineAsync($"[INFO] - Got: '{hashString}'", token);
        
        return string.Equals(hashString, expectedMd5.ToLowerInvariant());
    }

    private async Task<string?> FetchMd5Hash(string downloadUrl, CancellationToken token)
    {
        var finalUrl = downloadUrl + ".xml";

        try
        {
            var xmlContent = await _httpClient.GetStringAsync(finalUrl, token);
            var xmlDoc = XDocument.Parse(xmlContent);
            var md5Value = xmlDoc.Element("file")?.Attribute("md5")?.Value;
            
            await Console.Out.WriteLineAsync($"[INFO] - Fetched MD5 hash: '{md5Value}'", token);
            
            return md5Value;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARNING] - Failed to fetch or parse MD5 manifest from {finalUrl}: {e.Message}", token);
            return null;
        }
    }

    private void CreateOrUpdateDownloadManifest(DownloadTask downloadTask)
    {
        var manifestPath = Path.Combine(downloadTask.DestinationFolderPath, ManifestFileName);

        lock (_manifestWriteLock)
        {
            DownloadManifest? jsonData = null; 
        
            if (File.Exists(manifestPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(manifestPath);
                    jsonData = JsonSerializer.Deserialize<DownloadManifest>(jsonContent);
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine($"[WARNING] - Failed to parse download manifest file: {e.Message}]");
                    File.Delete(manifestPath);
                }
            }

            jsonData ??= new DownloadManifest
            {
                GameId = downloadTask.GameId,
                GameSlug = downloadTask.GameSlug,
                Platform = downloadTask.Platform,
                DownloadDirectoryPath = downloadTask.DestinationFolderPath,
                Language = downloadTask.Language ?? string.Empty,
            };

            var downloadedFileInfo = new DownloadedFile(
                Name: downloadTask.TargetFileName,
                Size: downloadTask.TotalBytes,
                Md5Hash: downloadTask.Md5Sum ?? string.Empty
            );
        
            jsonData.Files.Add(downloadedFileInfo);
            _downloadedCache[downloadTask.GameSlug] = jsonData;
        
            // Writing back to the file:
            var jsonText = JsonSerializer.Serialize(jsonData, _serializerOptions);
            File.WriteAllText(manifestPath, jsonText);
        }
    }

    private void HandleTaskCancellation(DownloadTask task)
    {
        CleanUpPartialFile(task);

        lock (_manifestWriteLock)
        {
            task.Status = EDownloadStatus.Cancelled;
                
            if (!IsDownloadingGame(task.GameId))
            {
                if (LibraryService.Instance.TryGetGameEntry(task.GameId, out var gameEntry))
                {
                    ClearDownloadedFiles(gameEntry);
                }
                else
                {
                    LibraryService.Instance.SetGameState(task.GameId, EGameState.Available);
                }
            }
                
            NotifyUi(() => TaskUpdated?.Invoke(task));
        }
    }
    
    // Properties:
    public static DownloadQueueService Instance { get; } = new();
    public IReadOnlyList<DownloadTask> AllTasks => [.. _allTasks];

    // The events that can be registered to:
    public event Action<DownloadTask>? TaskAdded;
    public event Action<DownloadTask>? TaskUpdated;
    public event Action<DownloadTask>? TaskCompleted;
}