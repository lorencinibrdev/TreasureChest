using TreasureChest.Source.Helpers;

namespace TreasureChest.Source.Services.Downloads;

public readonly record struct DownloadedFile(
    string Name,
    long Size,
    string Md5Hash
);

public class DownloadManifest
{
    // Properties:
    public required long GameId { get; init; }
    public required string GameSlug { get; init; }
    public required string DownloadDirectoryPath { get; init; }
    public required ESystemPlatform Platform { get; init; }
    public string Language { get; init; } = string.Empty;
    public List<DownloadedFile> Files { get; } = [];
}