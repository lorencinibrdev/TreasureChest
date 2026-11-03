namespace TreasureChest.Source.Services.Downloads;

public enum EDownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Validating,
    Failed,
    Cancelled
}