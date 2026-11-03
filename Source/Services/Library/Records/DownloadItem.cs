namespace TreasureChest.Source.Services.Library;

public readonly record struct DownloadItem(
    string Name,
    string Version,
    string Date,
    string Size,
    string ManualUrl
);