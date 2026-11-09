using System.Diagnostics;
using Adw;
using Gtk;
using MessageDialog = Adw.MessageDialog;

namespace TreasureChest.Source.Dialogs;

public class ExternalLinkConfirmationDialog
{
    // Private Members:
    private readonly MessageDialog _dialog;
    
    public ExternalLinkConfirmationDialog(MainWindow parent, string url)
    {
        var extraBox = Box.New(Orientation.Vertical, 12);
        extraBox.MarginTop = 12;
        extraBox.MarginBottom = 12;
        
        var titleLabel = Label.New(url);
        titleLabel.Wrap = true;
        titleLabel.Justify = Justification.Center;
        titleLabel.AddCssClass("title-4");

        var warningLabel = Label.New(I18n._("It will open on your default external browser."));
        warningLabel.Wrap = true;
        warningLabel.Justify = Justification.Center;
        warningLabel.AddCssClass("accent");
        
        extraBox.Append(titleLabel);
        extraBox.Append(warningLabel);
        
        _dialog = MessageDialog.New(
            parent: parent,
            heading: I18n._("Open External Link?"),
            body: I18n._("This link will lead you to an external website. Do you want to follow it?"));

        _dialog.ExtraChild = extraBox;
        
        _dialog.AddResponse("cancel", I18n._("Cancel"));
        _dialog.AddResponse("open_link", I18n._("Open Link"));
        
        _dialog.SetResponseAppearance("open_link", ResponseAppearance.Suggested);
        _dialog.SetDefaultResponse("open_link");
        _dialog.SetCloseResponse("cancel");

        _dialog.OnResponse +=  async(_, args) =>
        {
            if (args.Response != "open_link") 
                return;
            
            try
            {
                Process.Start("xdg-open", url);
            }
            catch (Exception)
            {
                await Console.Out.WriteLineAsync("[WARN] - Failed to open the requested link in the default system browser.");
            }
        };
    }

    public void Present()
    {
        _dialog.Present();
    }
}