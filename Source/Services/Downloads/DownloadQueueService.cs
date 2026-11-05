using System.Collections.Concurrent;
using System.Xml.Linq;

namespace TreasureChest.Source.Services.Downloads;

public class DownloadQueueService
{
    // Private Members:
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphoreSlim = new(2, 2);
    private readonly ConcurrentBag<DownloadTask> _allTasks = [];

    private DownloadQueueService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromHours(2)
        };
    }

    public async Task<DownloadTask> EnqueueDownload(long gameId, string title, string slug, string url, string targetFileName)
    {
        var targetDir = Path.Join(SettingsService.Instance.DownloadsFolder, slug);

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var task = new DownloadTask
        {
            GameId = gameId,
            Title = title,
            DownloadUrl = url,
            TargetFileName = targetFileName,
            DestinationFolderPath = targetDir
        };
        
        // Trying to fetch the md5 for later verification:
        task.Md5Sum = await FetchMd5Hash(url, task.Cts.Token);
        
        _allTasks.Add(task);
        
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

    public IEnumerable<DownloadTask> GetTasksForGame(long gameId)
    {
        return _allTasks.Where(t => t.GameId == gameId);
    }

    private async Task ProcessTaskAsync(DownloadTask task)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
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
                    bufferSize: 81920,
                    useAsync: true);

                var buffer = new byte[81920];
                int bytesRead;
                var lastReportTime = DateTime.UtcNow;
                long lastReportBytes = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), task.Cts.Token)) >
                       0)
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

            if (File.Exists(task.FinalFilePath))
            {
                File.Delete(task.FinalFilePath);
            }

            File.Move(task.PartialFilePath, task.FinalFilePath);

            task.Status = EDownloadStatus.Completed;
            NotifyUi(() =>
            {
                TaskUpdated?.Invoke(task);
                TaskCompleted?.Invoke(task);
            });
        }
        catch (OperationCanceledException)
        {
            task.Status = EDownloadStatus.Cancelled;
            CleanUpPartialFile(task);
            NotifyUi(() => TaskUpdated?.Invoke(task));
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public static void CancelDownload(DownloadTask task)
    {
        if (task.Status is EDownloadStatus.Downloading or EDownloadStatus.Queued)
        {
            task.Cts.Cancel();
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
        await Console.Out.WriteLineAsync($"[INFO] - Checking MD5 hash for file: '{filePath}'");
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
        
        await Console.Out.WriteLineAsync($"[INFO] - Expected: '{expectedMd5}'");
        await Console.Out.WriteLineAsync($"[INFO] - Got: '{hashString}'");
        
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
            
            await Console.Out.WriteLineAsync($"[INFO] - Fetched MD5 hash: '{md5Value}'");
            
            return md5Value;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARNING] - Failed to fetch or parse MD5 manifest from {finalUrl}: {e.Message}");
            return null;
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