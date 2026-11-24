namespace TreasureChest.Source.Services.Library.Records;

public record GamePlayStats(
    TimeSpan TimePlayed,
    DateTimeOffset? LastPlayedDate
);