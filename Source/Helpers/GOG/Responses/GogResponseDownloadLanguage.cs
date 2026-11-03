namespace TreasureChest.Source.Helpers.GOG.Responses;

public class GogResponseDownloadLanguage
{
    public string Language { get; init; } = string.Empty;
    public GogResponseDownloadPlatforms Platforms { get; init; } = new();
}