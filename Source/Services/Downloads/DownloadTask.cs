namespace TreasureChest.Source.Services.Downloads;

public enum EDownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public class DownloadTask
{
    // Properties:
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string TargetFileName { get; init; } = string.Empty;
    public string DestinationFolderPath { get; init; } = string.Empty;
    public EDownloadStatus Status { get; set; } = EDownloadStatus.Queued;
    public long TotalBytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double Progress => TotalBytes > 0 ? (double)TotalBytesDownloaded / TotalBytes : 0;
    public double SpeedBytesPerSecond { get; set; }
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource Cts { get; } = new();
    public string PartialFilePath => Path.Combine(DestinationFolderPath, $"{TargetFileName}.part");
    public string FinalFilePath => Path.Combine(DestinationFolderPath, TargetFileName);
}