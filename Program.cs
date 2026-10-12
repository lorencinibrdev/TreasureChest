using Gio;
using TreasureChest.Source.Services;
using TreasureChest.Source.Windows;
using Application = Adw.Application;

// Initializing the configuration service:
if (!ConfigurationService.Initialize())
{
    throw new Exception("Failed to initialize the configuration service. This should not happen...");
}

var app = Application.New(ConfigurationService.ApplicationId, ApplicationFlags.DefaultFlags);

app.OnActivate += (sender, _) =>
{
    var window = MainWindow.New((Application)sender);
    app.OnShutdown += window.HandleApplicationExit;
    app.OnShutdown += (_, _) => ConfigurationService.FinishExecution();
    window.Present();
};

return app.RunWithSynchronizationContext(null);