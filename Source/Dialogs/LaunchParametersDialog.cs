using Gtk;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Dialogs;

public class LaunchParametersDialog
{
    // Private Members:
    private readonly Adw.Dialog _modalDialog;
    private readonly MainWindow _parentWindow;

    public LaunchParametersDialog(MainWindow parent, GameEntry gameEntry)
    {
        _parentWindow = parent;
        
        _modalDialog = Adw.Dialog.New();
        _modalDialog.Title = $"Launch Parameters for '{gameEntry.Title}'";
        _modalDialog.ContentWidth = 480;
        _modalDialog.ContentHeight = 640;

        var headerBar = Adw.HeaderBar.New();
        var contentBox = Box.New(Orientation.Vertical, 0);
        contentBox.Append(headerBar);
    }

    public void Present()
    {
        _modalDialog.Present(_parentWindow);
    }
}