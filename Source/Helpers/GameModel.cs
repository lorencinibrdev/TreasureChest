using GObject;
using TreasureChest.Source.Services.Library;
using Object = GObject.Object;

namespace TreasureChest.Source.Helpers;

public enum EGameState
{
    Available,
    Downloading,
    Downloaded,
    Installing,
    Installed,
    Running
}

[Subclass<Object>]
public partial class GameModel
{
    public static GameModel New(GameEntry game)
    {
        var model = NewWithProperties([]);
        model.Id = game.Id;
        model.Slug = game.Slug;
        model.Title = game.Title;
        model.LogoPath = game.ImageLinks.Logo2X;
        model.IconPath = game.ImageLinks.SidebarIcon2X;
        model.SystemCompatibility = game.PlatformCompatibility;
        return model;
    }
    
    // Properties:
    public long Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string LogoPath { get; private set; } = string.Empty;
    public string IconPath { get; private set; } = string.Empty;
    public SystemCompatibility SystemCompatibility { get; private set; }
}