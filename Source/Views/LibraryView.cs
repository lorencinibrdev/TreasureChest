using Adw;
using Gtk;

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
        return box;
    }
}