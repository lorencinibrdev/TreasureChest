using System.Runtime.InteropServices;
using Gio;
using TreasureChest.Source.Services;
using Application = Adw.Application;
using File = System.IO.File;
using MainWindow = TreasureChest.Source.MainWindow;

// Loading the dconf schema:
var localSchemaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

if (Directory.Exists(localSchemaDir) && File.Exists(Path.Combine(localSchemaDir, "gschemas.compiled")))
{
    _ = NativeSetEnv("GSETTINGS_SCHEMA_DIR", localSchemaDir, 1);
}

GLib.Module.Initialize();
GLib.Functions.SetPrgname(SettingsService.ApplicationId);
Module.Initialize();

try
{
    var gresourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "TreasureChest.gresource");
    var resource = Functions.ResourceLoad(gresourcePath);
    Functions.ResourcesRegister(resource);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to load the gresouce: {e.Message}");
}

var app = Application.New(SettingsService.ApplicationId, ApplicationFlags.DefaultFlags);

app.OnActivate += (sender, _) =>
{
    var window = MainWindow.New((Application)sender);
    app.OnShutdown += window.HandleApplicationExit;
    window.Present();
};

// Loading some extra CSS effects:
var cssProvider = Gtk.CssProvider.New();
cssProvider.LoadFromString(@"
    .grayscale { filter: grayscale(100%); }
    .sepia { filter: sepia(100%); }
    .scrim-overlay { 
        background-image: linear-gradient(
            to bottom,
            alpha(@window_bg_color, 0) 40%,
            alpha(@window_bg_color, 0.8) 80%,
            @window_bg_color 100%
        );
    }
    .text-fade-gradient {
        /* Fades from transparent to the solid card background color */
        background-image: linear-gradient(to bottom, alpha(@card_bg_color, 0), @card_bg_color);
    }
");

Gtk.StyleContext.AddProviderForDisplay(
    Gdk.Display.GetDefault()!,
    cssProvider,
    800
);

return app.RunWithSynchronizationContext(null);

// Native P/Invoke to update the C runtime environment block directly
[DllImport("libc", EntryPoint = "setenv", CallingConvention = CallingConvention.Cdecl)]
static extern int NativeSetEnv(string name, string value, int overwrite);