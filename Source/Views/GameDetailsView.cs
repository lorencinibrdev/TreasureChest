using System.Globalization;
using System.Text.RegularExpressions;
using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Launcher;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Library.Records;
using TreasureChest.Source.Views.Library;

namespace TreasureChest.Source.Views;

public partial class GameDetailsView : IView
{
    // Private Members:
    private readonly MainWindow _parentWindow;
    private readonly Overlay _rootOverlay;
    private readonly PreferencesGroup _dlcGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly Label _descriptionLabel;
    private readonly Stack _mainButtonStack;
    private readonly GameEntry _game;
    private readonly Adw.Spinner _loadingSpinner;
    private readonly Box _contentBox;
    private uint _installProgressPulseTimeoutId;
    
    // Download Fields:
    private ProgressBar? _downloadProgressBar;
    private Label? _downloadStatusLabel;
    
    // The PlayStats for the game:
    private readonly Label _timePlayedLabel;
    private readonly Label _lastPlayedLabel;
    private readonly Box _lastPlayedBox;

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

        _loadingSpinner = Adw.Spinner.New();
        _loadingSpinner.SetSizeRequest(48, 48);
        _loadingSpinner.Halign = Align.Center;
        _loadingSpinner.Valign = Align.Center;
        _loadingSpinner.MarginTop = 64;
        _loadingSpinner.Visible = true;
        
        // The game description section:
        var textCard = Box.New(Orientation.Vertical, 0);
        textCard.AddCssClass("card");

        var cardContentBox = Box.New(Orientation.Vertical, 12);
        cardContentBox.MarginTop = 20;
        cardContentBox.MarginBottom = 20;
        cardContentBox.MarginStart = 24;
        cardContentBox.MarginEnd = 24;
        
        var aboutHeader = Label.New("<b>About the Game</b>");
        aboutHeader.UseMarkup = true;
        aboutHeader.Xalign = 0;
        cardContentBox.Append(aboutHeader);

        _descriptionLabel = Label.New(null);
        _descriptionLabel.Wrap = true;
        _descriptionLabel.Xalign = 0;
        _descriptionLabel.Selectable = true;
        _descriptionLabel.UseMarkup = true;
        _descriptionLabel.OnActivateLink += (_, args) =>
        {
            var confirmDialog = new ExternalLinkConfirmationDialog(_parentWindow, args.Uri);
            confirmDialog.Present();
            return true;
        };
        
        cardContentBox.Append(_descriptionLabel);
        textCard.Append(cardContentBox);
        
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

        var headerBar = Adw.HeaderBar.New();
        headerBar.AddCssClass("flat");
        headerBar.Valign = Align.Start;
        
        // Back button for returning to the library view:
        var backButton = Button.NewFromIconName("go-previous-symbolic");
        backButton.TooltipText = "Back to Library";
        backButton.OnClicked += (_, _) => _parentWindow.NavigateBackToLibrary();
        headerBar.PackStart(backButton);
        
        // The stats for the game:
        var statsBox = Box.New(Orientation.Horizontal, 16);
        statsBox.Valign = Align.Center;
        statsBox.MarginStart = 8;
        
        // Time Played:
        var timePlayedBox = Box.New(Orientation.Horizontal, 6);
        var clockIcon = Image.NewFromIconName("preferences-system-time-symbolic");
        clockIcon.PixelSize = 16;
        clockIcon.AddCssClass("dim-label");
        
        _timePlayedLabel = Label.New(FormatPlayTime(_game.PlayStats.TimePlayed));
        _timePlayedLabel.AddCssClass("caption-heading");
        _timePlayedLabel.AddCssClass("dim-label");
        _timePlayedLabel.AddCssClass("numeric");
        
        timePlayedBox.Append(clockIcon);
        timePlayedBox.Append(_timePlayedLabel);
        
        // Last Played:
        _lastPlayedBox = Box.New(Orientation.Horizontal, 6);
        var calendarIcon = Image.NewFromIconName("x-office-calendar-symbolic");
        calendarIcon.PixelSize = 16;
        calendarIcon.AddCssClass("dim-label");
        
        _lastPlayedLabel = Label.New(FormatLastPlayedDate(_game.PlayStats.LastPlayedDate));
        _lastPlayedLabel.AddCssClass("caption-heading");
        _lastPlayedLabel.AddCssClass("dim-label");
        _lastPlayedLabel.AddCssClass("numeric");
        
        _lastPlayedBox.Append(calendarIcon);
        _lastPlayedBox.Append(_lastPlayedLabel);
        
        statsBox.Append(timePlayedBox);
        statsBox.Append(_lastPlayedBox);

        _lastPlayedBox.Visible = _game.PlayStats.LastPlayedDate is not null;
        
        _rootOverlay.AddOverlay(headerBar);
        _ = FetchGameDescriptionAsync();
        _ = FetchGameContentAsync();
        _ = LoadBackgroundImageAsync(pictureBg);

        _mainButtonStack = Stack.New();
        _mainButtonStack.TransitionType = StackTransitionType.Crossfade;
        _mainButtonStack.Halign = Align.Start;
        
        UpdateMainButtonStack();
        
        controlsBox.Append(_mainButtonStack);
        controlsBox.Append(statsBox);
            
        LibraryService.Instance.GameStateChanged += HandleGameStateChanged;
        LibraryService.Instance.GamePlayStatsChanged += HandleGamePlayStatsChanged;
        DownloadQueueService.Instance.TaskUpdated += HandleDownloadProgressUpdate;
        
        RootWidget.OnUnrealize += (_, _) =>
        {
            LibraryService.Instance.GameStateChanged -= HandleGameStateChanged;
            LibraryService.Instance.GamePlayStatsChanged -= HandleGamePlayStatsChanged;
            DownloadQueueService.Instance.TaskUpdated -= HandleDownloadProgressUpdate;

            if (_installProgressPulseTimeoutId == 0) 
                return;
            
            GLib.Functions.SourceRemove(_installProgressPulseTimeoutId);
            _installProgressPulseTimeoutId = 0;
        };
    }

    private void HandleGamePlayStatsChanged(long gameId, GamePlayStats gamePlayStats)
    {
        if (_game.Id != gameId)
            return;
        
        GLib.Functions.IdleAdd(0, () =>
        {
            _timePlayedLabel.SetText(FormatPlayTime(gamePlayStats.TimePlayed));
            _lastPlayedLabel.SetText(FormatLastPlayedDate(gamePlayStats.LastPlayedDate));
            _lastPlayedBox.Visible = gamePlayStats.LastPlayedDate is not null;
            return false;
        });
    }

    private static string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime.TotalMinutes < 1)
            return "Never Played";

        if (Math.Abs(playTime.TotalMinutes - 1f) <= 1)
            return $"{(int)playTime.TotalMinutes} minute";
        
        return playTime.TotalHours < 1
            ? $"{(int)playTime.TotalMinutes} minutes"
            : $"{(int)playTime.TotalHours:F1} hours";
    }
    
    private static string FormatLastPlayedDate(DateTimeOffset? lastPlayedDate)
    {
        if (lastPlayedDate is null)
            return "Never";

        var localDate = lastPlayedDate.Value.ToLocalTime().Date;
        var today = DateTimeOffset.Now.Date;

        if (localDate == today)
            return "Today";

        return localDate == today.AddDays(-1)
            ? "Yesterday"
            : localDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private void HandleDownloadProgressUpdate(DownloadTask downloadTask)
    {
        if (downloadTask.GameId != _game.Id)
            return;
        
        GLib.Functions.IdleAdd(0, () =>
        {
            if (_downloadProgressBar is null || _downloadStatusLabel is null)
                return false;
            
            var gameTasks = DownloadQueueService.Instance.GetTasksForGame(_game.Id);

            if (gameTasks.Count == 0)
                return false;

            var totalBytesSum = gameTasks.Sum(t => t.TotalBytes);
            var totalDownloadedBytesSum = gameTasks.Sum(t => t.TotalBytesDownloaded);
            var progress = totalBytesSum > 0 ? (double)totalDownloadedBytesSum / totalBytesSum : 0;

            _downloadProgressBar.Fraction = progress;
            _downloadStatusLabel.SetText($"Downloading... {progress:P1}");

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

                splitButton.Label = "Install Downloaded Game";
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
                ConfirmUninstallDialog.Show(_parentWindow, _game);
            };
            
            var configureLaunchParametersButton = Button.NewWithLabel("Change Launch Parameters");

            configureLaunchParametersButton.OnClicked += (_, _) =>
            {
                installedGameOptionsPopover.Popdown();
                var modal = new LaunchParametersDialog(_parentWindow, _game);
                modal.Present();
            };
            
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
                _ = LauncherService.LaunchGameAsync(_game, CancellationToken.None);
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
            // The button:
            var installSplitButton = SplitButton.New();
            installSplitButton.AddCssClass("suggested-action");
            installSplitButton.AddCssClass("pill");
            installSplitButton.Label = "Install Game";
            
            // The menu button, with the popover options:
            var downloadedGameOptionsPopover = Popover.New();
            var downloadedGameOptionsBox = Box.New(Orientation.Vertical, 6);
            
            // The buttons on the popover:
            var clearDownloadedDataButton = Button.NewWithLabel("Clear Downloaded Data");
            downloadedGameOptionsBox.Append(clearDownloadedDataButton);
            
            clearDownloadedDataButton.OnClicked += (_, _) =>
            {
                ConfirmClearDownloadedData(_game);
            };
            
            downloadedGameOptionsPopover.Child = downloadedGameOptionsBox;
            
            installSplitButton.Popover = downloadedGameOptionsPopover;
            installSplitButton.OnClicked += (_, _) =>
            {
                if (!DownloadQueueService.Instance.TryGetDownloadManifest(_game.Slug, out var downloadManifest))
                    return;
                
                _ = InstallerService.Instance.InstallAsync(_game, downloadManifest);
            };

            _mainButtonStack.AddNamed(installSplitButton, nameof(EGameState.Downloaded));
        }
        
        // Checking for the downloading state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Downloading)) is null)
        {
            var downloadingBox = Box.New(Orientation.Horizontal, 12);
            downloadingBox.Valign = Align.Center;

            var progressInfoBox = Box.New(Orientation.Vertical, 6);
            progressInfoBox.Valign = Align.Center;
            
            _downloadProgressBar = ProgressBar.New();
            _downloadProgressBar.ShowText = false;
            _downloadProgressBar.SetSizeRequest(220, -1);

            _downloadStatusLabel = Label.New("Downloading... 0.0%");
            _downloadStatusLabel.Xalign = 0;
            _downloadStatusLabel.AddCssClass("caption-heading");
            _downloadStatusLabel.AddCssClass("numeric");
            
            progressInfoBox.Append(_downloadStatusLabel);
            progressInfoBox.Append(_downloadProgressBar);
            
            var cancelButton = Button.NewFromIconName("process-stop-symbolic");
            cancelButton.TooltipText = "Cancel Download";
            cancelButton.Valign = Align.Center;
            cancelButton.AddCssClass("circular");
            cancelButton.AddCssClass("osd");
            
            cancelButton.OnClicked += (_, _) =>
            {
                DownloadQueueService.CancelDownload(_game.Id, _game.Slug);
            };
            
            downloadingBox.Append(progressInfoBox);
            downloadingBox.Append(cancelButton);

            _mainButtonStack.AddNamed(downloadingBox, nameof(EGameState.Downloading));
        }
        
        // Checking for the installing state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Installing)) is not ProgressBar)
        {
            var installProgressBar = ProgressBar.New();
            installProgressBar.SetSizeRequest(220, -1);
            installProgressBar.Text = "Installing...";
            installProgressBar.ShowText = true;
            installProgressBar.Vexpand = false;

            _mainButtonStack.AddNamed(installProgressBar, nameof(EGameState.Installing));
        }
        
        // Checking for the uninstalling state:
        if (_mainButtonStack.GetChildByName(nameof(EGameState.Uninstalling)) is not ProgressBar)
        {
            var uninstallProgressBar = ProgressBar.New();
            uninstallProgressBar.SetSizeRequest(220, -1);
            uninstallProgressBar.Text = "Uninstalling...";
            uninstallProgressBar.ShowText = true;
            uninstallProgressBar.Vexpand = false;

            _mainButtonStack.AddNamed(uninstallProgressBar, nameof(EGameState.Uninstalling));
        }
        
        _mainButtonStack.SetVisibleChildName(currentGameState.ToString());
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
        var responseGameDescription = await GogService.Instance.FetchGameDescriptionAsync(_game.Id);

        if (responseGameDescription?.Description is null ||
            string.IsNullOrWhiteSpace(responseGameDescription.Description.Lead) ||
            string.IsNullOrWhiteSpace(responseGameDescription.Description.Full))
            return;
        
        // Trying to parse GOG's HTML to a label:
        var pangoMarkup = ConvertGogHtmlToPango(responseGameDescription.Description.Lead);

        try
        {
            if (Pango.Functions.ParseMarkup(pangoMarkup, -1, 0, out _, out _, out _))
            {
                _descriptionLabel.SetMarkup(pangoMarkup);
            }
        }
        catch (Exception)
        {
            var plainText = AllTags().Replace(pangoMarkup, string.Empty);
            _descriptionLabel.SetText(System.Net.WebUtility.HtmlDecode(plainText));
        }

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
            
            foreach (var platform in availablePlatforms.Reverse())
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
                        platformContent.Platform,
                        language,
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
    
    private void ConfirmClearDownloadedData(GameEntry gameEntry)
    {
        var extraBox = Box.New(Orientation.Vertical, 12);
        extraBox.MarginTop = 12;
        extraBox.MarginBottom = 12;
        
        var titleLabel = Label.New(gameEntry.Title);
        titleLabel.AddCssClass("title-4");

        var warningLabel = Label.New(I18n._("This will delete all downloaded files."));
        warningLabel.Wrap = true;
        warningLabel.Justify = Justification.Center;
        warningLabel.AddCssClass("warning");
        
        extraBox.Append(titleLabel);
        extraBox.Append(warningLabel);
        
        var dialog = Adw.MessageDialog.New(
            parent: _parentWindow,
            heading: I18n._("Clear Downloaded Installer Data?"),
            body: I18n._("Are you sure you want to completely delete the downloaded installers for the game?"));

        dialog.ExtraChild = extraBox;
        
        dialog.AddResponse("cancel", I18n._("Cancel"));
        dialog.AddResponse("clear", I18n._("Clear"));
        
        dialog.SetResponseAppearance("clear", ResponseAppearance.Destructive);
        dialog.SetDefaultResponse("cancel");
        dialog.SetCloseResponse("cancel");

        dialog.OnResponse +=  (_, args) =>
        {
            if (args.Response == "clear")
            {
                DownloadQueueService.Instance.ClearDownloadedFiles(gameEntry);
            }
        };
        
        dialog.Present();
    }

    private void PopulateContentGroups(GameContentEntry contentEntry)
    {
        // Handling the DLCs:
        _dlcGroup.Visible = contentEntry.Dlcs.Count > 0;

        foreach (var dlcContent in contentEntry.Dlcs)
        {
            var languagesAvailable = string.Join(", ", dlcContent.Downloads.Keys);
                
            var row = CreateDownloadRow(dlcContent.Title, "DLC Content", languagesAvailable, string.Empty, false);
            _dlcGroup.Add(row);
        }

        // Handling the extras:
        _extrasGroup.Visible = contentEntry.Extras.Count > 0;
            
        foreach (var extraContent in contentEntry.Extras)
        {
            var row = CreateDownloadRow(extraContent.Name, extraContent.Type, extraContent.Size, extraContent.ManualUrl, false);
            _extrasGroup.Add(row);
        }
    }
    
    private ActionRow CreateDownloadRow(string title, string typeLabel, string? size, string manualUrl, bool enableDownload)
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

        if (!enableDownload)
        {
            downloadButton.TooltipText = "Base game not downloaded. Download it first.";
        }
        
        downloadButton.Sensitive = enableDownload;

        downloadButton.OnClicked += async (_, _) =>
        {
            try
            {
                if (!DownloadQueueService.Instance.TryGetDownloadManifest(_game.Slug, out var downloadManifest))
                {
                    await Console.Out.WriteLineAsync($"[ERROR] - Base game is not downloaded. Cannot download extra content: {manualUrl}");
                    return;
                }
                
                var finalUrl = await GogService.Instance.ResolveFinalDownloadUrlAsync(manualUrl);
                var uri = new Uri(finalUrl);
                var fileName = Path.GetFileName(uri.AbsolutePath);
            
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    await Console.Out.WriteLineAsync($"[ERROR] - Failed to determine the downloadable content target filename: {manualUrl}");
                    return;
                }
            
                await Console.Out.WriteLineAsync($"[INFO] - Manual URL: {manualUrl}");
                await Console.Out.WriteLineAsync($"[INFO] - Download URL: {finalUrl}");
            
                _ = await DownloadQueueService.Instance.EnqueueDownload(
                    gameId: _game.Id,
                    title: $"{title} ({row.Subtitle})",
                    _game.Slug,
                    downloadManifest.Platform,
                    downloadManifest.Language,
                    url: finalUrl,
                    targetFileName: fileName
                );
            }
            catch
            {
                // ignored
            }
        };

        row.AddSuffix(pillBox);
        row.AddSuffix(downloadButton);
        return row;
    }
    
    private static string ConvertGogHtmlToPango(string html)
    {
        var text = html;
        
        text = HtmlRemoveImgTags().Replace(text, string.Empty);
        text = HtmlRemoveVideoTags().Replace(text, string.Empty);
        text = HtmlRemoveSourceTags().Replace(text, string.Empty);
        text = HtmlRemoveClosureVideoTags().Replace(text, string.Empty);
        text = HtmlRemoveHrTags().Replace(text, string.Empty);
        text = BrToNewLine().Replace(text, "\n");
        text = ParagraphBreakToNewLine().Replace(text, "\n\n");
        text = RemainingParagraphTags().Replace(text, string.Empty);
        text = ListItemToBullet().Replace(text, "\n• ");
        text = ListContainersAndClosures().Replace(text, string.Empty);
        text = StrongToBold().Replace(text, "<$1b>");
        text = EmToItalic().Replace(text, "<$1i>");
        text = CleanAnchorTags().Replace(text, "<a href=$1>");
        text = UnsupportedHtmlTags().Replace(text, string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = UnescapedAmpersand().Replace(text, "&amp;");

        return text.Trim();
    }
    
    // Properties:
    public string Identifier => "GameDetailsView";
    public Widget RootWidget => _rootOverlay;

    // Pre-compiled GeneratedRegex patterns:
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex HtmlRemoveImgTags();
    [GeneratedRegex(@"<video\b[^>]*>.*?</video>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex HtmlRemoveVideoTags();
    [GeneratedRegex("</video>", RegexOptions.IgnoreCase)] private static partial Regex HtmlRemoveClosureVideoTags();
    [GeneratedRegex(@"<source\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex HtmlRemoveSourceTags();
    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex HtmlRemoveHrTags();
    [GeneratedRegex(@"<\s*br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BrToNewLine();
    [GeneratedRegex(@"<\s*/p\s*>\s*<\s*p[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ParagraphBreakToNewLine();
    [GeneratedRegex(@"<\s*/?p[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex RemainingParagraphTags();
    [GeneratedRegex(@"<\s*li[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ListItemToBullet();
    [GeneratedRegex(@"<\s*/?(li|ul|ol)[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ListContainersAndClosures(); 
    [GeneratedRegex(@"<\s*(/?)\s*strong\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex StrongToBold();
    [GeneratedRegex(@"<\s*(/?)\s*em\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex EmToItalic();
    [GeneratedRegex(@"<(?!/?(b|i|u|a)\b)[^>]+>", RegexOptions.IgnoreCase)] private static partial Regex UnsupportedHtmlTags();
    [GeneratedRegex(@"&(?!amp;|lt;|gt;|quot;|apos;)")] private static partial Regex UnescapedAmpersand();
    [GeneratedRegex(@"<a\b[^>]*?\bhref\s*=\s*([""'][^""']*[""'])[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex CleanAnchorTags();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex AllTags();
}