using Gio;
using TreasureChest.Source.Services;
using TreasureChest.Source.Windows;
using Application = Adw.Application;

// Initializing the configuration service:
if (!ConfigurationService.Initialize())
{
    throw new Exception("Failed to initialize the configuration service. This should not happen...");
}

GLib.Module.Initialize();
GLib.Functions.SetPrgname(ConfigurationService.ApplicationId);
Module.Initialize();

try
{
    var resource = Functions.ResourceLoad("TreasureChest.gresource");
    Functions.ResourcesRegister(resource);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to load the gresouce: {e.Message}");
}

var app = Application.New(ConfigurationService.ApplicationId, ApplicationFlags.DefaultFlags);

app.OnActivate += (sender, _) =>
{
    var window = MainWindow.New((Application)sender);
    app.OnShutdown += window.HandleApplicationExit;
    app.OnShutdown += (_, _) => ConfigurationService.FinishExecution();
    window.Present();
};

// Loading some extra CSS effects:
var cssProvider = Gtk.CssProvider.New();
cssProvider.LoadFromString(@"
    .grayscale { filter: grayscale(100%); }
    .sepia { filter: sepia(100%); }
");

Gtk.StyleContext.AddProviderForDisplay(
    Gdk.Display.GetDefault()!,
    cssProvider,
    800
);

return app.RunWithSynchronizationContext(null);