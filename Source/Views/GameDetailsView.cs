using Adw;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Views.Library;
using HeaderBar = Adw.HeaderBar;
using Spinner = Gtk.Spinner;

namespace TreasureChest.Source.Views;

public class GameDetailsView : IView
{
    // Private Members:
    private readonly MainWindow _parentWindow;
    private readonly Overlay _rootOverlay;
    private readonly PreferencesGroup _dlcGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly Label _descriptionLabel;
    private readonly Stack _mainButtonStack;
    
    private readonly GameEntry _game;
    private GameContentEntry? _gameContent;

    private readonly Spinner _loadingSpinner;
    private readonly Box _contentBox;
    private string _fullGameDescription = string.Empty;

    public GameDetailsView(MainWindow parentWindow, GameEntry game)
    {
        _parentWindow = parentWindow;
        _game = game;
        
        _rootOverlay = Overlay.New();
        _rootOverlay.AddCssClass("background");
        
        // Entire view should be scrollable:
        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.HscrollbarPolicy = PolicyType.Never;
        scrolledWindow.Vexpand = true;

        var scrollableBox = Box.New(Orientation.Vertical, 0);
        scrolledWindow.SetChild(scrollableBox);
        
        // The overlay (for drawing the background picture in the actual background of the modal window):
        var overlay = Overlay.New();
        overlay.SetSizeRequest(-1, 340);
        
        // The background picture:
        var pictureBg = Picture.New();
        pictureBg.ContentFit = ContentFit.Cover;
        overlay.Child = pictureBg;

        var scrimBox = Box.New(Orientation.Vertical, 0);
        scrimBox.AddCssClass("scrim-overlay");
        overlay.AddOverlay(scrimBox);

        var controlsBox = Box.New(Orientation.Vertical, 16);
        controlsBox.Valign = Align.End;
        controlsBox.Halign = Align.Start;
        controlsBox.MarginStart = 32;
        controlsBox.MarginEnd = 32;
        
        // The label for the game title:
        var titleLabel = Label.New(_game.Title);
        titleLabel.AddCssClass("title-1");
        titleLabel.Halign = Align.Start;
        titleLabel.Wrap = true;
        
        controlsBox.Append(titleLabel);
        overlay.AddOverlay(controlsBox);
        scrollableBox.Append(overlay);
        
        // The content for downloads and such:
        var contentClamp = Clamp.New();
        contentClamp.MaximumSize = 1000;
        contentClamp.MarginTop = 32;
        contentClamp.MarginBottom = 48;
        contentClamp.MarginStart = 24;
        contentClamp.MarginEnd = 24;

        _loadingSpinner = Spinner.New();
        _loadingSpinner.SetSizeRequest(48, 48);
        _loadingSpinner.Halign = Align.Center;
        _loadingSpinner.Valign = Align.Center;
        _loadingSpinner.MarginTop = 64;
        _loadingSpinner.Visible = true;
        _loadingSpinner.Spinning = true;
        
        // The game description section:
        var textCard = Box.New(Orientation.Vertical, 0);
        textCard.AddCssClass("card");

        var aboutHeader = Label.New("<b>About the Game</b>");
        aboutHeader.UseMarkup = true;
        aboutHeader.Xalign = 0;
        aboutHeader.MarginTop = 16;
        aboutHeader.MarginBottom = 8;
        aboutHeader.MarginStart = 16;
        aboutHeader.MarginEnd = 16;
        textCard.Append(aboutHeader);
        
        _descriptionLabel = Label.New(string.Empty);
        _descriptionLabel.Wrap = true;
        _descriptionLabel.Xalign = 0;
        _descriptionLabel.Yalign = 0;
        _descriptionLabel.MarginStart = 16;
        _descriptionLabel.MarginEnd = 16;

        textCard.Append(_descriptionLabel);

        var readMoreButton = Button.New();
        readMoreButton.AddCssClass("card");
        readMoreButton.MarginBottom = 24;
        readMoreButton.OnClicked += (_, _) => OpenFullDescriptionModal(_fullGameDescription);

        var buttonContent = Box.New(Orientation.Horizontal, 8);
        buttonContent.Halign = Align.Center;
        buttonContent.MarginTop = 14;
        buttonContent.MarginBottom = 14;

        var btnLabel = Label.New("<b>Read Full Description</b>");
        btnLabel.UseMarkup = true;
        var btnIcon = Image.NewFromIconName("go-next-symbolic");
        
        buttonContent.Append(btnLabel);
        buttonContent.Append(btnIcon);
        readMoreButton.Child = buttonContent;
        
        _contentBox = Box.New(Orientation.Vertical, 24);
        _contentBox.Visible = false;
        
        _dlcGroup = PreferencesGroup.New();
        _dlcGroup.Title = "DLC's";
        _dlcGroup.Description = "Downloadable content for this game.";
        _dlcGroup.Visible = false;
        
        _extrasGroup = PreferencesGroup.New();
        _extrasGroup.Title = "Extras";
        _extrasGroup.Description = "Arts, Music and other extras.";
        _extrasGroup.Visible = false;

        _contentBox.Append(textCard);
        _contentBox.Append(readMoreButton);
        _contentBox.Append(_dlcGroup);
        _contentBox.Append(_extrasGroup);

        var centralLayout = Box.New(Orientation.Vertical, 0);
        centralLayout.Append(_loadingSpinner);
        centralLayout.Append(_contentBox);
        contentClamp.Child = centralLayout;

        scrollableBox.Append(contentClamp);

        _rootOverlay.Child = scrolledWindow;

        var headerBar = HeaderBar.New();
        headerBar.AddCssClass("flat");
        headerBar.Valign = Align.Start;
        
        // Back button for returning to the library view:
        var backButton = Button.NewFromIconName("go-previous-symbolic");
        backButton.TooltipText = "Back to Library";
        backButton.OnClicked += (_, _) => _parentWindow.NavigateBackToLibrary();
        headerBar.PackStart(backButton);
        
        _rootOverlay.AddOverlay(headerBar);
        _ = FetchGameContentAsync();
        _ = LoadBackgroundImageAsync(pictureBg);

        _mainButtonStack = Stack.New();
        _mainButtonStack.TransitionType = StackTransitionType.Crossfade;
        _mainButtonStack.Halign = Align.Start;
        
        UpdateMainButtonStack();
        
        controlsBox.Append(_mainButtonStack);
            
        LibraryService.Instance.GameStateChanged += HandleGameStateChanged;
        DownloadQueueService.Instance.TaskUpdated += HandleDownloadProgressUpdate;
        
        RootWidget.OnUnrealize += (_, _) =>
        {
            LibraryService.Instance.GameStateChanged -= HandleGameStateChanged;
            DownloadQueueService.Instance.TaskUpdated -= HandleDownloadProgressUpdate;
        };
    }

    private void HandleDownloadProgressUpdate(DownloadTask downloadTask)
    {
        if (downloadTask.GameId != _game.Id)
            return;
        
        GLib.Functions.IdleAdd(0, () =>
        {
            if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloading)) is not ProgressBar progressBar)
                return false;
            
            progressBar.Fraction = downloadTask.Progress;
            progressBar.Text = $"{downloadTask.Progress:0.1}%";

            return false;
        });
    }

    private void HandleGameStateChanged(long gameId, EGameState newState)
    {
        if (_game.Id != gameId)
            return;
        
        GLib.Functions.IdleAdd(0, () =>
        {
            _mainButtonStack.SetVisibleChildName(newState.ToString());
            return false;
        });
    }

    private void UpdateMainButtonStack()
    {
        // Checking the current state of the game:
        var currentGameState = LibraryService.Instance.GetGameState(_game.Id);

        // Do we have the play button already?
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Installed)) is not SplitButton)
        {
            // The button:
            var playSplitButton = SplitButton.New();
            playSplitButton.Label = "Play!";
            playSplitButton.AddCssClass("suggested-action");
            playSplitButton.AddCssClass("pill");
            
            // The menu button, with the popover options:
            var installedGameOptionsPopover = Popover.New();
            var installedGameOptionsBox = Box.New(Orientation.Vertical, 0);
            installedGameOptionsBox.Append(Button.NewWithLabel("Uninstall"));
            installedGameOptionsBox.Append(Button.NewWithLabel("Change Launch Parameters"));
            installedGameOptionsPopover.Child = installedGameOptionsBox;
            
            playSplitButton.Popover = installedGameOptionsPopover;
            playSplitButton.OnClicked += (_, _) => Console.Out.WriteLine("[INFO] - Launching game...");

            _mainButtonStack.AddNamed(playSplitButton, nameof(EGameState.Installed));
        }
        
        // Now, checking for the available state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Available)) is not MenuButton)
        {
            // The download options MenuButton, with it's popover:
            var downloadMenuButton = MenuButton.New();
            downloadMenuButton.Label = "Download";
            downloadMenuButton.AddCssClass("suggested-action");
            downloadMenuButton.AddCssClass("pill");
            downloadMenuButton.Sensitive = false;
            
            _mainButtonStack.AddNamed(downloadMenuButton, nameof(EGameState.Available));
        }
        
        // Checking for the downloaded state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloaded)) is not SplitButton)
        {
            // The button:
            var installSplitButton = SplitButton.New();
            installSplitButton.Label = "Install Game";
            installSplitButton.AddCssClass("suggested-action");
            installSplitButton.AddCssClass("pill");
            
            // The menu button, with the popover options:
            var downloadedGameOptionsPopover = Popover.New();
            var downloadedGameOptionsBox = Box.New(Orientation.Vertical, 6);
            downloadedGameOptionsBox.Append(Button.NewWithLabel("Clear Downloaded Data"));
            downloadedGameOptionsPopover.Child = downloadedGameOptionsBox;
            
            installSplitButton.Popover = downloadedGameOptionsPopover;
            installSplitButton.OnClicked += (_, _) => Console.Out.WriteLine("[INFO] - Installing game...");

            _mainButtonStack.AddNamed(installSplitButton, nameof(EGameState.Downloaded));
        }
        
        // Checking for the downloading state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloading)) is not ProgressBar)
        {
            var downloadProgressBar = ProgressBar.New();
            downloadProgressBar.ShowText = true;
            downloadProgressBar.MarginTop = 8;
            downloadProgressBar.MarginBottom = 8;
            downloadProgressBar.Vexpand = false;

            _mainButtonStack.AddNamed(downloadProgressBar, nameof(EGameState.Downloading));
        }
        
        // Checking for the installing state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Installing)) is not ProgressBar)
        {
            var installProgressBar = ProgressBar.New();
            installProgressBar.Text = "Installing...";
            installProgressBar.ShowText = true;
            installProgressBar.MarginTop = 8;
            installProgressBar.MarginBottom = 8;
            installProgressBar.Vexpand = false;
            installProgressBar.Pulse();

            _mainButtonStack.AddNamed(installProgressBar, nameof(EGameState.Installing));
        }
        
        _mainButtonStack.SetVisibleChildName(currentGameState.ToString());
    }
    
    private void OpenFullDescriptionModal(string fullDescription)
    {
        
    }
    
    private async Task LoadBackgroundImageAsync(Picture bgPicture)
    {
        var localBgImagePath = await ImagePrefetchService.Instance.FetchImageWithPriorityAsync(_game.Id, EImageType.Background, _game.ImageLinks.Background);

        if (!File.Exists(localBgImagePath))
            return;
        
        bgPicture.SetFilename(localBgImagePath);
    }

    private async Task FetchGameContentAsync()
    {
        var responseGameContent = await GogService.Instance.FetchGameContentDetails(_game);
        _gameContent = GameContentEntry.TryCreate(responseGameContent);

        if (_gameContent is null)
            return;

        // Updating the download button:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Available)) is not MenuButton downloadButton)
            return;
        
        PopulateDownloadPopover(downloadButton, _gameContent);
    }
    
    private void PopulateDownloadPopover(MenuButton downloadButton, GameContentEntry gameContent)
    {
        var downloadPopover = Popover.New();
            
        var downloadPopoverBox = Box.New(Orientation.Vertical, 4);
        downloadPopoverBox.AddCssClass("popover");
        downloadPopoverBox.SetSizeRequest(300, -1);
        downloadPopoverBox.MarginTop = 8;
        downloadPopoverBox.MarginBottom = 8;
        downloadPopoverBox.MarginStart = 8;
        downloadPopoverBox.MarginEnd = 8;

        var listBox = ListBox.New();
        listBox.SelectionMode = SelectionMode.None;
        listBox.AddCssClass("boxed-list");

        foreach (var (languageString, availablePlatforms) in gameContent.Downloads)
        {
            var expanderRow = ExpanderRow.New();
            expanderRow.Title = languageString;

            var (windowsIcon, linuxIcon, macOsIcon) = LibraryView.LoadPlatformIcons();
            windowsIcon.Visible = false;
            linuxIcon.Visible = false;
            macOsIcon.Visible = false;
            
            foreach (var platform in availablePlatforms)
            {
                switch (platform.Platform)
                {
                    case ESystemPlatform.Windows:
                        windowsIcon.Visible = true;
                        break;
                    case ESystemPlatform.Linux:
                        linuxIcon.Visible = true;
                        break;
                    case ESystemPlatform.MacOs:
                        macOsIcon.Visible = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var platformRow = ActionRow.New();
                platformRow.Title = $"Download for {platform.Platform.ToString()}";
                platformRow.Subtitle = $"{platform.Items.Count} file(s)";
                platformRow.Activatable = true;
                platformRow.Halign = Align.Fill;

                platformRow.OnActivated += (_, _) =>
                {
                    downloadPopover.Popdown();
                    HandleStartDownloads(languageString, platform);
                };
                
                expanderRow.AddRow(platformRow);
            }

            var iconsBox = Box.New(Orientation.Horizontal, 6);
            iconsBox.Valign = Align.Center;
            iconsBox.MarginEnd = 8;
            
            iconsBox.Append(windowsIcon);
            iconsBox.Append(linuxIcon);
            iconsBox.Append(macOsIcon);

            expanderRow.AddSuffix(iconsBox);
            listBox.Append(expanderRow);
        }

        downloadPopoverBox.Append(listBox);
        downloadPopover.Child = downloadPopoverBox;
        downloadButton.Popover = downloadPopover;
        downloadButton.Sensitive = true;
    }

    private async void HandleStartDownloads(string language, PlatformContent platformContent)
    {
        try
        {
            // Fetching the list of files:
            if (platformContent.Items.Count == 0)
                return;

            var fileCountStr = $"{platformContent.Items.Count} file";

            if (platformContent.Items.Count > 1)
            {
                fileCountStr += "s";
            }
        
            // Queueing the download of all files:
            foreach (var downloadItem in platformContent.Items)
            {
                try
                {
                    var finalUrl = await GogService.Instance.ResolveFinalDownloadUrlAsync(downloadItem.ManualUrl);
                    var uri = new Uri(finalUrl);
                    var fileName = Path.GetFileName(uri.AbsolutePath);
                
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        fileName = $"{_game.Title.Replace(" ", "_")}_{platformContent.Platform.ToString()}.bin";
                    }
            
                    await Console.Out.WriteLineAsync($"[INFO] - Manual URL: {downloadItem.ManualUrl}");
                    await Console.Out.WriteLineAsync($"[INFO] - Download URL: {finalUrl}");

                    var downloadTask = await DownloadQueueService.Instance.EnqueueDownload(
                        gameId: _game.Id,
                        title: $"{downloadItem.Name} ({downloadItem.Size})",
                        _game.Slug,
                        url: finalUrl,
                        targetFileName: fileName
                    );
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync($"[ERROR] - Failed to queue the requested download: {e.Message}");
                }
            }
        
            _parentWindow.ShowToastNotification($"Queued '{_game.Title.EscapeMarkup()}', in {language}, for {platformContent.Platform.ToString()} [{fileCountStr}]");
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    // Properties:
    public string Identifier => "GameDetailsView";
    public Widget RootWidget => _rootOverlay;
}