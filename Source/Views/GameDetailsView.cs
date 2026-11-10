using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Launcher;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Views.Library;
using WebKit;
using HeaderBar = Adw.HeaderBar;
using MessageDialog = Adw.MessageDialog;
using Spinner = Adw.Spinner;

namespace TreasureChest.Source.Views;

public class GameDetailsView : IView
{
    // Private Members:
    private readonly MainWindow _parentWindow;
    private readonly Overlay _rootOverlay;
    private readonly PreferencesGroup _dlcGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly ScrolledWindow _scrolledWindowDescription;
    private readonly Stack _mainButtonStack;
    
    private readonly GameEntry _game;

    private readonly Spinner _loadingSpinner;
    private readonly Box _contentBox;

    private uint _installProgressPulseTimeoutId;

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
        contentClamp.MaximumSize = 1920;
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

        _scrolledWindowDescription = ScrolledWindow.New();
        _scrolledWindowDescription.MarginStart = 12;
        _scrolledWindowDescription.MarginEnd = 12;
        _scrolledWindowDescription.MarginTop = 12;
        _scrolledWindowDescription.MarginEnd = 12;
        _scrolledWindowDescription.HasFrame = false;
        _scrolledWindowDescription.MinContentHeight = 600;
        textCard.Append(_scrolledWindowDescription);
        
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
        
        var downloadableBox = Box.New(Orientation.Horizontal, 24);
        downloadableBox.Append(_dlcGroup);
        downloadableBox.Append(_extrasGroup);
        
        _contentBox.Append(textCard);
        _contentBox.Append(downloadableBox);
        
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
        _ = FetchGameDescriptionAsync();
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

            if (_installProgressPulseTimeoutId == 0) 
                return;
            
            GLib.Functions.SourceRemove(_installProgressPulseTimeoutId);
            _installProgressPulseTimeoutId = 0;
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
            progressBar.Text = $"{downloadTask.Progress * 10:0.1}%";

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

            if (newState == EGameState.Installing && _installProgressPulseTimeoutId == 0)
            {
                _installProgressPulseTimeoutId = GLib.Functions.TimeoutAdd(0, 100, () => 
                    {
                        if (_mainButtonStack.GetChildByName(nameof(EGameState.Installing)) is not ProgressBar progressBar) 
                            return false;
                        
                        progressBar.Pulse();
                        return true;
                    }
                );
            }
            else if (newState != EGameState.Installing && _installProgressPulseTimeoutId != 0)
            {
                GLib.Functions.SourceRemove(_installProgressPulseTimeoutId);
                _installProgressPulseTimeoutId = 0;
            }
            else if (newState == EGameState.Downloaded)
            {
                if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloaded)) is not SplitButton splitButton)
                    return false;
                
                var setupFilesPath = Path.Join(SettingsService.Instance.DownloadsFolder, _game.Slug);
                var (installationType, _) = InstallerService.IdentifyGameInstallationType(_game.Slug, setupFilesPath);

                switch (installationType)
                {
                    case EGameInstallationType.LinuxNative:
                        splitButton.Label = "Install Game (Linux Native)";
                        break;
                    case EGameInstallationType.WindowsInstaller:
                        splitButton.Label = "Install Game (Windows)";
                        break;
                    case EGameInstallationType.MacOsInstaller:
                        splitButton.Label = "Install Game (MacOS Native)";
                        break;
                    case EGameInstallationType.Unknown:
                    default:
                        splitButton.Label = "Install Game (Unknown Installer Type)";
                        break;
                }
            }
            
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
            // The popover data:
            var installedGameOptionsPopover = Popover.New();
            var installedGameOptionsBox = Box.New(Orientation.Vertical, 12);
            installedGameOptionsPopover.Child = installedGameOptionsBox;
         
            var uninstallButton = Button.NewWithLabel("Uninstall");

            uninstallButton.OnClicked += (_, _) =>
            {
                installedGameOptionsPopover.Popdown();
                ConfirmUninstall(_game);
            };
            
            var configureLaunchParametersButton = Button.NewWithLabel("Change Launch Parameters");
            
            installedGameOptionsBox.Append(uninstallButton);
            installedGameOptionsBox.Append(configureLaunchParametersButton);
            
            // The button:
            var playSplitButton = SplitButton.New();
            playSplitButton.Label = "Play!";
            playSplitButton.AddCssClass("suggested-action");
            playSplitButton.AddCssClass("pill");
            playSplitButton.Popover = installedGameOptionsPopover;
            playSplitButton.OnClicked += (_, _) =>
            {
                _ = LauncherService.Instance.LaunchGameAsync(_game);
            };

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
        
        // Now, checking for the running state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Running)) is not MenuButton)
        {
            // The download options MenuButton, with it's popover:
            var runningMenuButton = MenuButton.New();
            runningMenuButton.Label = "Running...";
            runningMenuButton.AddCssClass("suggested-action");
            runningMenuButton.AddCssClass("pill");
            runningMenuButton.Sensitive = false;
            
            _mainButtonStack.AddNamed(runningMenuButton, nameof(EGameState.Running));
        }
        
        // Checking for the downloaded state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloaded)) is not SplitButton)
        {
            var setupFilesPath = Path.Join(SettingsService.Instance.DownloadsFolder, _game.Slug);
            
            // The button:
            var installSplitButton = SplitButton.New();
            installSplitButton.AddCssClass("suggested-action");
            installSplitButton.AddCssClass("pill");
            
            // Trying to quickly fetch the game install type for the downloaded files:
            var (installationType, _) = InstallerService.IdentifyGameInstallationType(_game.Slug, setupFilesPath);

            switch (installationType)
            {
                case EGameInstallationType.LinuxNative:
                    installSplitButton.Label = "Install Game (Linux Native)";
                    break;
                case EGameInstallationType.WindowsInstaller:
                    installSplitButton.Label = "Install Game (Windows)";
                    break;
                case EGameInstallationType.MacOsInstaller:
                    installSplitButton.Label = "Install Game (MacOS Native)";
                    break;
                case EGameInstallationType.Unknown:
                default:
                    installSplitButton.Label = "Install Game (Unknown Installer Type)";
                    break;
            }
            
            // The menu button, with the popover options:
            var downloadedGameOptionsPopover = Popover.New();
            var downloadedGameOptionsBox = Box.New(Orientation.Vertical, 6);
            downloadedGameOptionsBox.Append(Button.NewWithLabel("Clear Downloaded Data"));
            downloadedGameOptionsPopover.Child = downloadedGameOptionsBox;
            
            installSplitButton.Popover = downloadedGameOptionsPopover;
            installSplitButton.OnClicked += (_, _) =>
            {
                _ = InstallerService.Instance.InstallAsync(_game, setupFilesPath);
            };

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

            _mainButtonStack.AddNamed(installProgressBar, nameof(EGameState.Installing));
        }
        
        // Checking for the uninstalling state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Uninstalling)) is not ProgressBar)
        {
            var uninstallProgressBar = ProgressBar.New();
            uninstallProgressBar.Text = "Uninstalling...";
            uninstallProgressBar.ShowText = true;
            uninstallProgressBar.MarginTop = 8;
            uninstallProgressBar.MarginBottom = 8;
            uninstallProgressBar.Vexpand = false;

            _mainButtonStack.AddNamed(uninstallProgressBar, nameof(EGameState.Uninstalling));
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
        var gameContentEntry = GameContentEntry.TryCreate(responseGameContent);

        if (gameContentEntry is null)
            return;

        // Updating the download button:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Available)) is not MenuButton downloadButton)
            return;
        
        PopulateDownloadPopover(downloadButton, gameContentEntry);
        PopulateContentGroups(gameContentEntry);
    }

    private async Task FetchGameDescriptionAsync()
    {
        var responseGameDescription =await GogService.Instance.FetchGameDescriptionAsync(_game.Id);

        if (responseGameDescription?.Description is null ||
            string.IsNullOrWhiteSpace(responseGameDescription.Description.Lead) ||
            string.IsNullOrWhiteSpace(responseGameDescription.Description.Full))
            return;
        
        // Showing the html description render:
        CreateDescriptionHtmlWidget(responseGameDescription.Description.Lead);

        _loadingSpinner.Visible = false;
        _contentBox.Visible = true;
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

                    _ = await DownloadQueueService.Instance.EnqueueDownload(
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

    private void ConfirmUninstall(GameEntry gameEntry)
    {
        var extraBox = Box.New(Orientation.Vertical, 12);
        extraBox.MarginTop = 12;
        extraBox.MarginBottom = 12;
        
        var titleLabel = Label.New(gameEntry.Title);
        titleLabel.AddCssClass("title-4");

        var warningLabel = Label.New(I18n._("This will delete all installed files, possibly including your save game data."));
        warningLabel.Wrap = true;
        warningLabel.Justify = Justification.Center;
        warningLabel.AddCssClass("warning");
        
        extraBox.Append(titleLabel);
        extraBox.Append(warningLabel);
        
        var dialog = MessageDialog.New(
            parent: _parentWindow,
            heading: I18n._("Uninstall Game?"),
            body: I18n._("Are you sure you want to completely uninstall the game?"));

        dialog.ExtraChild = extraBox;
        
        dialog.AddResponse("cancel", I18n._("Cancel"));
        dialog.AddResponse("uninstall", I18n._("Uninstall"));
        
        dialog.SetResponseAppearance("uninstall", ResponseAppearance.Destructive);
        dialog.SetDefaultResponse("cancel");
        dialog.SetCloseResponse("cancel");

        dialog.OnResponse +=  async(_, args) =>
        {
            if (args.Response == "uninstall")
            {
                await InstallerService.Instance.UninstallAsync(gameEntry);
            }
        };
        
        dialog.Present();
    }

    private void CreateDescriptionHtmlWidget(string rawGogHtml)
    {
        var styledHtml = $$"""
                           <!DOCTYPE html>
                           <html>
                           <head>
                               <style>
                                   :root {
                                       color-scheme: dark light; /* Tells WebKit to respect system dark mode */
                                   }
                                   body {
                                       font-family: system-ui, sans-serif;
                                       background-color: transparent; /* Blends with GTK window background */
                                       font-size: 14px;
                                       line-height: 1.6;
                                       padding: 0;
                                       margin: 0;
                                   }
                                   img {
                                       max-width: 100%;
                                       height: auto;
                                       border-radius: 8px; /* GNOME HIG style rounding for images */
                                       margin-top: 12px;
                                       margin-bottom: 12px;
                                   }
                                   a {
                                       color: #3584e4; /* GNOME Accent Blue */
                                       text-decoration: none;
                                   }
                               </style>
                           </head>
                           <body>
                               {{rawGogHtml}}
                           </body>
                           </html>
                           """;

        var webView = WebView.New();
        webView.SetBackgroundColor(new Gdk.RGBA { Red = 0, Green = 0, Blue = 0, Alpha = 0 });
        webView.LoadHtml(styledHtml, null);

        webView.OnDecidePolicy += (_, args) =>
        {
            var decision = args.Decision;

            if (decision is NavigationPolicyDecision navigationDecision)
            {
                var type = navigationDecision.NavigationAction.GetNavigationType();
                var uri = navigationDecision.NavigationAction.GetRequest().Uri;

                if (type == NavigationType.LinkClicked)
                {
                    decision.Ignore();

                    if (string.IsNullOrWhiteSpace(uri)) 
                        return true;
                    
                    var confirmDialog = new ExternalLinkConfirmationDialog(_parentWindow, uri);
                    confirmDialog.Present();

                    return true;
                }
            }
            
            decision.Use();
            return true;
        };

        _scrolledWindowDescription.SetChild(webView);
        _scrolledWindowDescription.Vexpand = true;
    }

    private void PopulateContentGroups(GameContentEntry contentEntry)
    {
        // Handling the DLCs:
        _dlcGroup.Visible = contentEntry.Dlcs.Count > 0;

        foreach (var dlcContent in contentEntry.Dlcs)
        {
            var languagesAvailable = string.Join(", ", dlcContent.Downloads.Keys);
                
            var row = CreateDownloadRow(dlcContent.Title, "DLC Content", languagesAvailable);
            _dlcGroup.Add(row);
        }

        // Handling the extras:
        _extrasGroup.Visible = contentEntry.Extras.Count > 0;
            
        foreach (var extraContent in contentEntry.Extras)
        {
            var row = CreateDownloadRow(extraContent.Name, extraContent.Type, extraContent.Size);
            _extrasGroup.Add(row);
        }
    }
    
    private static ActionRow CreateDownloadRow(string title, string typeLabel, string? size)
    {
        var row = ActionRow.New();

        row.Title = System.Security.SecurityElement.Escape(title);
        
        if (!string.IsNullOrWhiteSpace(size))
        {
            row.Subtitle = System.Security.SecurityElement.Escape(size);
        }

        var pillBox = Box.New(Orientation.Horizontal, 0);
        pillBox.AddCssClass("card");
        pillBox.Valign = Align.Center;
        pillBox.MarginEnd = 12;

        var pillLabel = Label.New(typeLabel);
        pillLabel.AddCssClass("caption-heading");
        pillLabel.AddCssClass("dim-label");
        pillLabel.MarginStart = 10;
        pillLabel.MarginEnd = 10;
        pillLabel.MarginTop = 4;
        pillLabel.MarginBottom = 4;
        pillBox.Append(pillLabel);

        var downloadButton = Button.NewFromIconName("folder-download-symbolic");
        downloadButton.Valign = Align.Center;
        downloadButton.AddCssClass("flat");
        downloadButton.AddCssClass("circular");

        downloadButton.OnClicked += (_, _) =>
        {
            Console.Out.WriteLine($"[INFO] - Starting download for: {title}");
        };

        row.AddSuffix(pillBox);
        row.AddSuffix(downloadButton);
        return row;
    }
    
    // Properties:
    public string Identifier => "GameDetailsView";
    public Widget RootWidget => _rootOverlay;
}