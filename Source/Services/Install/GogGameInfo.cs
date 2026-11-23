using System.Text.Json.Serialization;
using TreasureChest.Source.Services.Runners;

namespace TreasureChest.Source.Services.Install;

public class GogGameInfo
{
    [JsonPropertyName("playTasks")] public List<GogPlayTaskEntry> PlayTasks { get; init; } = [];
}

public class GogPlayTaskEntry
{
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("isPrimary")] public bool IsPrimary { get; set; }
    [JsonPropertyName("isHidden")] public bool IsHidden { get; set; }
    [JsonPropertyName("languages")] public List<string> LanguagesList { get; set; } = [];
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;
    [JsonPropertyName("workingDir")] public string? WorkingDirectory { get; set; }
    public ERunnerType RunnerType { get; set; } = ERunnerType.Unknown;
}