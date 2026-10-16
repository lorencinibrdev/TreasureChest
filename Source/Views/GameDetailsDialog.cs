using Gtk;
using TreasureChest.Source.Helpers.GOG;
using Dialog = Adw.Dialog;
using HeaderBar = Adw.HeaderBar;
using Window = Gtk.Window;

namespace TreasureChest.Source.Views;

public class GameDetailsDialog
{
    // Private Members:
    private readonly Dialog _modalDialog;
    private readonly Window _parentWindow;
    
    public GameDetailsDialog(Window parentWindow, GameDetailsResponse selectedGame)
    {
        _parentWindow = parentWindow;
        
        _modalDialog = Dialog.New();
        _modalDialog.Title = $"{selectedGame.Title} - {selectedGame.Id}";
        _modalDialog.ContentWidth = 800;
        _modalDialog.ContentHeight = 600;

        var headerBar = HeaderBar.New();
        var contentBox = Box.New(Orientation.Vertical, 0);
        contentBox.Append(headerBar);
        
        _modalDialog.SetChild(contentBox);
    }

    public void Present()
    {
        _modalDialog.Present(_parentWindow);
    }
}