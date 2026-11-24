using Adw;
using Gtk;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Dialogs;

public static class ConfirmUninstallDialog
{
    public static void Show(MainWindow parent, GameEntry gameEntry)
    {
        var extraBox = Box.New(Orientation.Vertical, 12);
        extraBox.MarginTop = 12;
        extraBox.MarginBottom = 12;
        
        var titleLabel = Label.New(gameEntry.Title);
        titleLabel.AddCssClass("title-4");

        var warningLabel = Label.New(I18n._("This will delete all installed files, possibly including your save game data."));
        warningLabel.Wrap = true;
        warningLabel.Justify = Justification.Center;
        warningLabel.AddCssClass("warning");
        
        extraBox.Append(titleLabel);
        extraBox.Append(warningLabel);
        
        var dialog = Adw.MessageDialog.New(
            parent: parent,
            heading: I18n._("Uninstall Game?"),
            body: I18n._("Are you sure you want to completely uninstall the game?"));

        dialog.ExtraChild = extraBox;
        
        dialog.AddResponse("cancel", I18n._("Cancel"));
        dialog.AddResponse("uninstall", I18n._("Uninstall"));
        
        dialog.SetResponseAppearance("uninstall", ResponseAppearance.Destructive);
        dialog.SetDefaultResponse("cancel");
        dialog.SetCloseResponse("cancel");

        dialog.OnResponse +=  async(_, args) =>
        {
            if (args.Response == "uninstall")
            {
                await InstallerService.Instance.UninstallAsync(gameEntry);
            }
        };
        
        dialog.Present();
    }
}