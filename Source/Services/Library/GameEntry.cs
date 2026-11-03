using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Services.Library;

public class GameEntry
{
    private GameEntry(long id,
        string title,
        string slug,
        GogResponseSystemCompatibility systemCompatibility,
        GogResponseLinks links,
        bool isInstallable,
        string gameType,
        GogResponseImageLinks imageLinks,
        GogResponseDlcInformation dlcInformation)
    {
        Id = id;
        Title = title;
        Slug = slug;
        IsInstallable = isInstallable;
        Type = gameType;
        HasDlc = dlcInformation.HasDlc;

        PlatformCompatibility = new SystemCompatibility(
            systemCompatibility.Windows ?? false,
            systemCompatibility.MacOs ?? false,
            systemCompatibility.Linux ?? false
        );

        ImageLinks = new ImageLinks(
            imageLinks.Background ?? string.Empty,
            imageLinks.Logo ?? string.Empty,
            imageLinks.Logo2X ?? string.Empty,
            imageLinks.SidebarIcon ?? string.Empty,
            imageLinks.SideBarIcon2X ?? string.Empty,
            imageLinks.MenuNotificationAv ?? string.Empty,
            imageLinks.MenuNotificationAv2 ?? string.Empty,
            imageLinks.Icon ?? string.Empty
        );
        
        GameLinks = new GameLinks(
            links.Product ?? string.Empty,
            links.Support ?? string.Empty,
            links.Forum ?? string.Empty,
            dlcInformation.AllProductsUrl ?? string.Empty,
            dlcInformation.ExpandedAllProductsUrl ?? string.Empty
        );
    }

    public static GameEntry? TryCreate(GogResponseGameDetails? gameDetailsResponse)
    {
        if (gameDetailsResponse is null)
            return null;

        if (gameDetailsResponse.Id is null ||
            gameDetailsResponse.Title is null ||
            gameDetailsResponse.Slug is null ||
            gameDetailsResponse.SystemCompatibility is null ||
            gameDetailsResponse.Links is null ||
            gameDetailsResponse.GameType is null ||
            gameDetailsResponse.Images is null ||
            gameDetailsResponse.DlcInformation is null)
        {
            Console.Out.WriteLine("[WARN] - Received invalid game data from API. Dropping it...");
            return null;
        }

        return new GameEntry(gameDetailsResponse.Id.Value,
            gameDetailsResponse.Title,
            gameDetailsResponse.Slug,
            gameDetailsResponse.SystemCompatibility,
            gameDetailsResponse.Links,
            gameDetailsResponse.IsInstallable ?? false,
            gameDetailsResponse.GameType,
            gameDetailsResponse.Images,
            gameDetailsResponse.DlcInformation);
    }
    
    // Properties:
    public long Id { get; }
    public string Title { get; }
    public string Slug { get; }
    public string Type { get; }
    public SystemCompatibility PlatformCompatibility { get; }
    public GameLinks GameLinks { get; }
    public ImageLinks ImageLinks { get; }
    public bool IsInstallable { get; }
    public bool HasDlc { get; }
}