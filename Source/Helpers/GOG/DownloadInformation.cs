namespace TreasureChest.Source.Helpers.GOG;

public enum ESupportedPlatform
{
    Windows,
    Linux,
    MacOs
}

public class FileInformation(GogDownloadItem downloadItem)
{
    // Properties:
    public string FileName { get; } = downloadItem.Name ?? string.Empty;
    public string ManualUrl { get; } = downloadItem.ManualUrl ?? string.Empty;
    public string Size { get; } = downloadItem.Size ?? string.Empty;
    public string Version { get; } = downloadItem.Version ?? string.Empty;
    public string Date { get; } = downloadItem.Date ?? string.Empty;
}

public class DownloadInformation
{
    // Private Members:
    private readonly Dictionary<ESupportedPlatform, List<FileInformation>> _downloads = [];
    
    public DownloadInformation(GogDownloadLanguage downloadLanguage)
    {
        Language = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(downloadLanguage.Language.ToLower());

        if (downloadLanguage.Platforms.Windows is not null)
        {
            var downloadItemList = (from downloadItem in downloadLanguage.Platforms.Windows
                where downloadItem.HasData
                select new FileInformation(downloadItem)).ToList();

            _downloads[ESupportedPlatform.Windows] = downloadItemList;
        }
        
        if (downloadLanguage.Platforms.Linux is not null)
        {
            var downloadItemList = (from downloadItem in downloadLanguage.Platforms.Linux
                where downloadItem.HasData
                select new FileInformation(downloadItem)).ToList();

            _downloads[ESupportedPlatform.Linux] = downloadItemList;
        }
        
        if (downloadLanguage.Platforms.Mac is not null)
        {
            var downloadItemList = (from downloadItem in downloadLanguage.Platforms.Mac
                where downloadItem.HasData
                select new FileInformation(downloadItem)).ToList();

            _downloads[ESupportedPlatform.MacOs] = downloadItemList;
        }
    }
    
    public List<FileInformation> GetDownloadsForPlatform(ESupportedPlatform platform)
    {
        return _downloads.TryGetValue(platform, out var fileList) 
            ? fileList 
            : [];
    }

    public List<ESupportedPlatform> GetSupportedPlatforms()
    {
        return [.. _downloads.Keys];
    }
    
    // Properties:
    public string Language { get; }
}