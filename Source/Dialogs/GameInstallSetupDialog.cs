using Adw;
using Gtk;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.Downloads;
using Dialog = Adw.Dialog;
using HeaderBar = Adw.HeaderBar;

namespace TreasureChest.Source.Dialogs;

public class GameInstallSetupDialog
{
    // Private Members:
    private readonly Dialog _dialog;
    private readonly MainWindow _parentWindow;
    private readonly StringList _languageModel;
    private readonly StringList _platformModel;
    private readonly ComboRow _languageRow;
    private readonly ComboRow _platformRow;
    private readonly Button _startDownloadButton;
    private readonly string _gameTitle;
    private readonly Dictionary<string, Dictionary<string, string>> _availableDownloads;

    public GameInstallSetupDialog(MainWindow parentWindow, string gameTitle, Dictionary<string, Dictionary<string, string>> availableDownloads)
    {
        _parentWindow = parentWindow;
        _availableDownloads = availableDownloads;
        _gameTitle = gameTitle;
        
        _dialog = Dialog.New();
        _dialog.Title = $"Install {gameTitle}";
        _dialog.ContentWidth = 800;
        _dialog.ContentHeight = 600;

        var toolbarView = ToolbarView.New();

        var headerBar = HeaderBar.New();
        
        var cancelButton = Button.NewWithLabel("Cancel");
        cancelButton.OnClicked += (_, _) => _dialog.Close();
        headerBar.PackStart(cancelButton);
        
        _startDownloadButton = Button.NewWithLabel("Start Download");
        _startDownloadButton.AddCssClass("suggested-action");
        _startDownloadButton.OnClicked += HandleStartDownload;
        headerBar.PackEnd(_startDownloadButton);
        
        toolbarView.AddTopBar(headerBar);
        
        var preferencesPage = PreferencesPage.New();
        var group = PreferencesGroup.New();
        group.Title = "Installation Options";
        group.Description = "Select your preferred language and platform installer.";

        _languageModel = StringList.New([.._availableDownloads.Keys]);
        _languageRow = ComboRow.New();
        _languageRow.Title = "Language";
        _languageRow.Model = _languageModel;

        _languageRow.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() == "selected-item")
            {
                UpdatePlatformOptions();
            }
        };

        _platformModel = StringList.New([]);
        _platformRow = ComboRow.New();
        _platformRow.Title = "Platform";
        _platformRow.Model = _platformModel;
        
        group.Add(_languageRow);
        group.Add(_platformRow);
        preferencesPage.Add(group);
        
        toolbarView.Content = preferencesPage;
        _dialog.Child = toolbarView;
        
        UpdatePlatformOptions();
    }

    public void Present()
    {
        _dialog.Present(_parentWindow);
    }

    private async void HandleStartDownload(Button sender, EventArgs args)
    {
        try
        {
            var selectedLang = _languageModel.GetString(_languageRow.Selected);
            var selectedPlatform = _platformModel.GetString(_platformRow.Selected);

            if (selectedLang is null || selectedPlatform is null)
            {
                _dialog.Close();
                return;
            }
        
            await Console.Out.WriteLineAsync($"[INFO] - User choose {selectedLang} - {selectedPlatform}.");
        
            var manualUrl = _availableDownloads[selectedLang][selectedPlatform];

            _startDownloadButton.Sensitive = false;
            _startDownloadButton.Label = "Resolving...";

            try
            {
                var finalUrl = await GogServices.ResolveFinalDownloadUrlAsync(manualUrl);

                var uri = new Uri(finalUrl);
                var fileName = Path.GetFileName(uri.AbsolutePath);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"{_gameTitle.Replace(" ", "_")}_{selectedPlatform}.bin";
                }
            
                await Console.Out.WriteLineAsync($"[INFO] - Manual URL: {manualUrl}");
                await Console.Out.WriteLineAsync($"[INFO] - Download URL: {finalUrl}");

                DownloadQueueService.Instance.EnqueueDownload(
                    title: $"{_gameTitle} ({selectedPlatform})",
                    url: finalUrl,
                    targetFileName: fileName
                );
                
                _parentWindow.ShowToastNotification($"Started downloading {_gameTitle}");
                _startDownloadButton.IconName = "object-select-symbolic";
                _startDownloadButton.Sensitive = false;
                
                _dialog.Close();
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"[ERROR] - Failed to queue the requested download: {e.Message}");
                _startDownloadButton.Sensitive = true;
                _startDownloadButton.Label = "Start Download";
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void UpdatePlatformOptions()
    {
        var selectedLangIndex = _languageRow.Selected;

        if (selectedLangIndex == Gtk.Constants.INVALID_LIST_POSITION)
            return;
        
        var selectedLang = _languageModel.GetString(selectedLangIndex);

        if (selectedLang is null || !_availableDownloads.TryGetValue(selectedLang, out var platforms))
            return;
        
        _platformModel.Splice(0, _platformModel.NItems, [..platforms.Keys]);

        var linuxIndex = Array.IndexOf([.. platforms.Keys], "linux");
        _platformRow.Selected = linuxIndex >= 0 ? (uint)linuxIndex : 0;
    }
}