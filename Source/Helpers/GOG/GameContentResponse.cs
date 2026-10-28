using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreasureChest.Source.Helpers.GOG;

public class GameContentResponse
{
    // Properties:
    [JsonPropertyName("cdKey")] public string? CdKey { get; init; }
    [JsonPropertyName("textInformation")] public string? TextInformation { get; init; }
    [JsonPropertyName("downloads")] [JsonConverter(typeof(GogDownloadsConverter))] public List<GogDownloadLanguage>? Downloads { get; init; }
    [JsonPropertyName("extras")] public List<GogExtra>? Extras { get; init; }
    [JsonPropertyName("dlcs")] [JsonConverter(typeof(GogDlcInfoConverter))] public DlcInformation? DlcInformation { get; init; }
    [JsonPropertyName("tags")] public List<object>? Tags { get; init; }
    [JsonPropertyName("isPreOrder")] public bool IsPreOrder { get; init; }
    [JsonPropertyName("releaseTimestamp")] public long ReleaseTimestamp { get; init; }
    [JsonPropertyName("messages")] public List<object>? Messages { get; init; } 
    [JsonPropertyName("changelog")] public string? Changelog { get; init; } 
    [JsonPropertyName("forumLink")] public string? ForumLink { get; init; }
    [JsonPropertyName("isBaseProductMissing")] public bool IsBaseProductMissing { get; init; }
    [JsonPropertyName("missingBaseProduct")] public object? MissingBaseProduct { get; init; }
}

public class GogExtra
{
    [JsonPropertyName("manualUrl")] public string? ManualUrl { get; init; }
    [JsonPropertyName("downloaderUrl")] public string? DownloaderUrl { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("info")] public int Info { get; init; }
    [JsonPropertyName("size")] public string? Size { get; init; }
}

public class GogDownloadLanguage
{
    public string Language { get; init; } = string.Empty;
    public GogDownloadPlatforms Platforms { get; init; } = new();
}

public class GogDownloadItem
{
    [JsonPropertyName("manualUrl")] public string? ManualUrl { get; init; }
    [JsonPropertyName("downloaderUrl")] public string? DownloaderUrl { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("date")] public string? Date { get; init; }
    [JsonPropertyName("size")] public string? Size { get; init; }
}

public class GogDownloadPlatforms
{
    [JsonPropertyName("windows")] public List<GogDownloadItem>? Windows { get; init; }
    [JsonPropertyName("mac")] public List<GogDownloadItem>? Mac { get; init; }
    [JsonPropertyName("linux")] public List<GogDownloadItem>? Linux { get; init; }
    public bool HasData => Windows is not null || Mac is not null || Linux is not null;
}

public class GogDownloadsConverter : JsonConverter<List<GogDownloadLanguage>>
{
    public override List<GogDownloadLanguage> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<GogDownloadLanguage>();

        using var document = JsonDocument.ParseValue(ref reader);
        
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var langArray in document.RootElement.EnumerateArray())
        {
            if (langArray.ValueKind != JsonValueKind.Array || langArray.GetArrayLength() != 2) 
                continue;
            
            var languageName = langArray[0].GetString() ?? "Unknown";
            var platformsObj = langArray[1].Deserialize<GogDownloadPlatforms>(options) ?? new GogDownloadPlatforms();

            result.Add(new GogDownloadLanguage
            {
                Language = languageName,
                Platforms = platformsObj
            });
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<GogDownloadLanguage> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        
        foreach (var langGroup in value)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(langGroup.Language);
            JsonSerializer.Serialize(writer, langGroup.Platforms, options);
            writer.WriteEndArray();
        }
        
        writer.WriteEndArray();
    }
}