using Gio;
using TreasureChest.Source.Services;
using TreasureChest.Source.Windows;
using Application = Adw.Application;

var configData = ConfigurationService.LoadConfiguration();
var app = Application.New(ConfigurationService.ApplicationId, ApplicationFlags.DefaultFlags);

app.OnActivate += (sender, _) =>
{
    var window = MainWindow.New((Application)sender, configData);
    app.OnShutdown += window.HandleApplicationExit;
    window.Present();
};

return app.RunWithSynchronizationContext(null);