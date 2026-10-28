using System.Collections.Concurrent;

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

    public DownloadTask EnqueueDownload(string title, string url, string targetFileName)
    {
        var targetDir = SettingsService.Instance.DownloadsFolder;

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var task = new DownloadTask
        {
            Title = title,
            DownloadUrl = url,
            TargetFileName = targetFileName,
            DestinationFolderPath = targetDir
        };
        
        _allTasks.Add(task);
        
        NotifyUi(() => TaskAdded?.Invoke(task));
        _ = ProcessTaskAsync(task);
        return task;
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

            await fileStream.DisposeAsync();

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

    public void CancelDownload(DownloadTask task)
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
    
    // Properties:
    public static DownloadQueueService Instance { get; } = new();
    public IReadOnlyList<DownloadTask> AllTasks => [.. _allTasks];
    
    // The events that can be registered to:
    public event Action<DownloadTask>? TaskAdded;
    public event Action<DownloadTask>? TaskUpdated;
    public event Action<DownloadTask>? TaskCompleted;
}