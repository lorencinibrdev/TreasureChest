namespace TreasureChest.Source.Services.Library;

public readonly record struct SystemCompatibility(
    bool Windows,
    bool MacOs,
    bool Linux
);