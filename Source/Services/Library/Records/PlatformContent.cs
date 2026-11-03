using TreasureChest.Source.Helpers;

namespace TreasureChest.Source.Services.Library;

public readonly record struct PlatformContent(
    ESystemPlatform Platform,
    IReadOnlyList<DownloadItem> Items
);