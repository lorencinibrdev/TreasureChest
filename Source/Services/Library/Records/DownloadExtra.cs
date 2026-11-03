namespace TreasureChest.Source.Services.Library;

public readonly record struct DownloadExtra(
    string Name,
    string Type,
    int Info,
    string Size,
    string ManualUrl
);