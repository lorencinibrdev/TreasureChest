using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Services.Library;

public class GameContentEntry
{
    private GameContentEntry(
        string title,
        string cdKey,
        IReadOnlyDictionary<string, IReadOnlyList<PlatformContent>> downloads,
        IReadOnlyList<DownloadExtra> extras,
        IReadOnlyList<GameContentEntry> dlcs)
    {
        Title = title;
        CdKey = cdKey;
        Downloads = downloads;
        Extras = extras;
        Dlcs = dlcs;
    }

    public static GameContentEntry? TryCreate(GogResponseGameContent? gameContentResponse)
    {
        if (gameContentResponse?.Title is null)
            return null;

        // Parsing the downloads:
        var parsedDownloads = new Dictionary<string, IReadOnlyList<PlatformContent>>();
        
        if (gameContentResponse.Downloads is not null)
        {
            foreach (var languageGroup in gameContentResponse.Downloads)
            {
                var platforms = new List<PlatformContent>();

                if (languageGroup.Platforms.Windows is { Count: > 0 })
                {
                    platforms.Add(new PlatformContent(ESystemPlatform.Windows, [.. languageGroup.Platforms.Windows.Select(MapDownloadItem)]));
                }
                
                if (languageGroup.Platforms.Mac is { Count: > 0 })
                {
                    platforms.Add(new PlatformContent(ESystemPlatform.MacOs, [.. languageGroup.Platforms.Mac.Select(MapDownloadItem)]));
                }
                
                if (languageGroup.Platforms.Linux is { Count: > 0 })
                {
                    platforms.Add(new PlatformContent(ESystemPlatform.Linux, [.. languageGroup.Platforms.Linux.Select(MapDownloadItem)]));
                }

                parsedDownloads[languageGroup.Language] = platforms;
            }
        }
        
        // PArsing the Extras:
        var parsedExtras = gameContentResponse.Extras?
            .Where(e => !string.IsNullOrEmpty(e.ManualUrl))
            .Select(e => new DownloadExtra(
                e.Name ?? "Unnamed Extra",
                e.Type ?? "Unknown Type",
                e.Info,
                e.Size ?? "0 MB",
                e.ManualUrl!
            )).ToList() ?? [];
        
        // Mapping the DLC's:
        var allDlcContent = gameContentResponse.DlcInformation?
            .Select(TryCreate)
            .OfType<GameContentEntry>()
            .ToList() ?? [];

        var playableDlcs = allDlcContent
            .Where(c => c.Downloads.Count != 0)
            .ToList();

        var nonPlayableDlcs = allDlcContent
            .Where(c => c.Downloads.Count == 0)
            .ToList();

        foreach (var nonPlayableDlc in nonPlayableDlcs)
        {
            parsedExtras.AddRange(nonPlayableDlc.Extras);
        }

        return new GameContentEntry(
            gameContentResponse.Title,
            gameContentResponse.CdKey ?? string.Empty,
            parsedDownloads,
            parsedExtras,
            playableDlcs
        );
    }

    private static DownloadItem MapDownloadItem(GogResponseDownloadItem downloadItemResponse)
    {
        return new DownloadItem(
            downloadItemResponse.Name ?? "Unknown File",
            downloadItemResponse.Version ?? string.Empty,
            downloadItemResponse.Date ?? string.Empty,
            downloadItemResponse.Size ?? "0 MB",
            downloadItemResponse.ManualUrl ?? string.Empty
        );
    }
    
    // Properties:
    public string Title { get; }
    public string CdKey { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PlatformContent>> Downloads { get; }
    public IReadOnlyList<DownloadExtra> Extras { get; }
    public IReadOnlyList<GameContentEntry> Dlcs { get; }
}