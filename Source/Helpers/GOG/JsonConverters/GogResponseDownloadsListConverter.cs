using System.Text.Json;
using System.Text.Json.Serialization;
using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Helpers.GOG.JsonConverters;

public class GogResponseDownloadsListConverter : JsonConverter<List<GogResponseDownloadLanguage>>
{
    public override List<GogResponseDownloadLanguage> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<GogResponseDownloadLanguage>();

        using var document = JsonDocument.ParseValue(ref reader);
        
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        result.AddRange(from langArray in document.RootElement.EnumerateArray()
            where langArray.ValueKind == JsonValueKind.Array && langArray.GetArrayLength() == 2
            let languageName = langArray[0].GetString() ?? "Unknown"
            let platformsObj = langArray[1].Deserialize<GogResponseDownloadPlatforms>(options) ?? new GogResponseDownloadPlatforms()
            select new GogResponseDownloadLanguage
            {
                Language = languageName,
                Platforms = platformsObj
            });

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<GogResponseDownloadLanguage> value, JsonSerializerOptions options)
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