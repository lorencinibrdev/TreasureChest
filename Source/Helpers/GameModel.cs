using GObject;
using TreasureChest.Source.Helpers.GOG;
using Object = GObject.Object;

namespace TreasureChest.Source.Helpers;

[Subclass<Object>]
public partial class GameModel
{
    public static GameModel New(GameDetailsResponse gameDetails)
    {
        var model = NewWithProperties([]);
        model.Id = gameDetails.Id ?? 0;
        model.Slug = gameDetails.Slug ?? string.Empty;
        model.Title = gameDetails.Title ?? "Unknown Title";
        model.LogoPath = gameDetails.Images?.Logo2X ?? string.Empty;
        model.IconPath = gameDetails.Images?.SideBarIcon2X ?? string.Empty;
        return model;
    }
    
    // Properties:
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
}