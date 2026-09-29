using Adw;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;

namespace TreasureChest;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Initialize Libadwaita & GTK application
        var app = Application.New("io.github.lorencinibrdev.TreasureChest", Gio.ApplicationFlags.FlagsNone);

        app.OnActivate += (sender, e) =>
        {
            // Create Application Window
            var window = ApplicationWindow.New((Application)sender);
            window.SetTitle("TreasureChest");
            window.SetDefaultSize(900, 600);

            // Create a Libadwaita Status Page widget
            var statusPage = StatusPage.New();
            statusPage.SetTitle("TreasureChest");
            statusPage.SetDescription("GOG Client for GNOME");
            statusPage.SetIconName("applications-games-symbolic");

            window.SetContent(statusPage);
            window.Present();
        };

        // Run application loop
        app.Run(args);
    }
}