using Adw;
using Gtk;
using TreasureChest.Source.Services;

namespace TreasureChest.Source.Views;

public class LibraryView
{
    public static Widget Create()
    {
        var box = Box.New(Orientation.Vertical, 0);
        box.Vexpand = true;

        var statusPage = StatusPage.New();
        statusPage.Title = "Your Library";
        statusPage.Description = "Games will appear here once authenticated.";
        statusPage.IconName = "emblem-photos-symbolic";

        box.Append(statusPage);
        
        var button = Button.NewWithLabel("Refresh Tokens");
        button.OnClicked += async (_, _) =>
        {
            await GogServices.RefreshTokens();
        };

        statusPage.SetChild(button);
        
        return box;
    }
}