using System.Text.RegularExpressions;
using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.Downloads;
using HeaderBar = Adw.HeaderBar;
using Spinner = Gtk.Spinner;

namespace TreasureChest.Source.Views;

public partial class GameDetailsView : IView
{
    // Private Members:
    private readonly MainWindow _parentWindow;
    private readonly Overlay _rootOverlay;
    private readonly PreferencesGroup _dlcsGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly Label _descriptionLabel;
    private readonly MenuButton _installButton;
    
    private readonly GameDetailsResponse _gameDetails;
    private GameContentResponse? _contentDetails;

    private readonly Spinner _loadingSpinner;
    private readonly Box _contentBox;

    private string _fullGameDescription = string.Empty;

    public GameDetailsView(MainWindow parentWindow, GameDetailsResponse gameDetails)
    {
        _parentWindow = parentWindow;
        _gameDetails = gameDetails;
        
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
        var titleLabel = Label.New(_gameDetails.Title);
        titleLabel.AddCssClass("title-1");
        titleLabel.Halign = Align.Start;
        titleLabel.Wrap = true;

        _installButton = MenuButton.New();
        _installButton.Label = "Install";
        _installButton.AddCssClass("suggested-action");
        _installButton.AddCssClass("pill");
        _installButton.Halign = Align.Start;
        _installButton.SetSizeRequest(140, -1);
        _installButton.Sensitive = false;
        
        controlsBox.Append(titleLabel);
        controlsBox.Append(_installButton);
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
        
        _dlcsGroup = PreferencesGroup.New();
        _dlcsGroup.Title = "DLC's";
        _dlcsGroup.Description = "Downloadable content for this game.";
        _dlcsGroup.Visible = false;
        
        _extrasGroup = PreferencesGroup.New();
        _extrasGroup.Title = "Extras";
        _extrasGroup.Description = "Arts, Music and other extras.";
        _extrasGroup.Visible = false;

        _contentBox.Append(textCard);
        _contentBox.Append(readMoreButton);
        _contentBox.Append(_dlcsGroup);
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
        
        _ = LoadImagesAsync(_gameDetails, pictureBg);
        _ = FetchGameDetailsAsync();
    }

    private void OpenFullDescriptionModal(string fullDescription)
    {
        var test = 09;
    }
    
    private async Task LoadImagesAsync(GameDetailsResponse selectedGame, Picture bgPicture)
    {
        var localBgImagePath = await ImagePrefetchService.FetchImageWithPriorityAsync(selectedGame.Id.ToString()!, EImageType.Background, selectedGame.Images?.Background);

        if (!File.Exists(localBgImagePath))
            return;
        
        bgPicture.SetFilename(localBgImagePath);
    }
    
    private async Task FetchGameDetailsAsync()
    {
        try
        {
            if (await GogServices.FetchGameContentDetails(_gameDetails) is not GameContentResponse gameContent || _gameDetails.Id is null)
            {
                _installButton.Label = "Game Unavailable";
                _installButton.Sensitive = false;
                return;
            }
        
            SetupInstallButton(gameContent);
        
            // Handling the description:
            var productDescription = await GogServices.FetchGameDescriptionAsync(_gameDetails.Id.Value);
            var leadDescription = productDescription?.Description?.Lead ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(leadDescription))
            {
                _fullGameDescription = CleanHtmlDescription(leadDescription);

                var previewText = _fullGameDescription;

                if (_fullGameDescription.Length > 350)
                {
                    var cutoffIndex = _fullGameDescription.LastIndexOf(' ', 350);
                    previewText = (cutoffIndex > 0 ? _fullGameDescription[..cutoffIndex] : _fullGameDescription[..350]) + "...";
                }
                
                _descriptionLabel.SetText(CleanHtmlDescription(previewText));
            }
        
            // Handling the DLCs:
            if (_gameDetails.DlcInformation is not null && _gameDetails.DlcInformation.HasDlc)
            {
                var dlcDetails = await GogServices.FetchGameDlcContentDetails(_gameDetails);

                if (dlcDetails is not null && dlcDetails.Count != 0)
                {
                    foreach (var downloadInfo in dlcDetails)
                    {
                        var row = CreateDownloadRow(downloadInfo.Title!, downloadInfo.GameType!, "BlaBlaBla");
                        _dlcsGroup.Add(row);
                    }

                    _dlcsGroup.Visible = true;
                }
            }

            // Handling the extras:
            if (gameContent.Extras is not null)
            {
                var hasExtras = false;
            
                foreach (var downloadInfo in gameContent.Extras)
                {
                    var row = CreateDownloadRow(downloadInfo.Name!, downloadInfo.Type!, downloadInfo.Size);
                    _extrasGroup.Add(row);
                    hasExtras = true;
                }
            
                _extrasGroup.Visible = hasExtras;
            }
        }
        finally
        {
            _loadingSpinner.Spinning = false;
            _loadingSpinner.Visible = false;
            _contentBox.Visible = true;
        }
    }

    private ActionRow CreateDownloadRow(string title, string typeLabel, string? size)
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
    
    private List<DownloadInformation> ExtractDownloadsList(GameContentResponse gameContent)
    {
        if (gameContent.Downloads is null)
            return [];
        
        var list = new List<DownloadInformation>();

        foreach (var downloadLanguageGroup in gameContent.Downloads)
        {
            if (string.IsNullOrWhiteSpace(downloadLanguageGroup.Language) || 
                !downloadLanguageGroup.Platforms.HasData)
                continue;

            list.Add(new DownloadInformation(downloadLanguageGroup));
        }

        return list;
    }

    private void SetupInstallButton(GameContentResponse contentDetails)
    {
        _installButton.Sensitive = true;
        
        var downloadsList = ExtractDownloadsList(contentDetails);

        var popover = Popover.New();
        
        var mainPopoverBox = Box.New(Orientation.Vertical, 4);
        mainPopoverBox.AddCssClass("popover");
        mainPopoverBox.SetSizeRequest(300, -1);
        mainPopoverBox.MarginTop = 8;
        mainPopoverBox.MarginBottom = 8;
        mainPopoverBox.MarginStart = 8;
        mainPopoverBox.MarginEnd = 8;

        var listBox = ListBox.New();
        listBox.SelectionMode = SelectionMode.None;
        listBox.AddCssClass("boxed-list");

        foreach (var downloadInformation in downloadsList)
        {
            var expanderRow = ExpanderRow.New();
            expanderRow.Title = downloadInformation.Language;

            var (windowsIcon, linuxIcon, macOsIcon) = LibraryView.LoadPlatformIcons();
            windowsIcon.Visible = false;
            linuxIcon.Visible = false;
            macOsIcon.Visible = false;
            
            foreach (var supportedPlatform in downloadInformation.GetSupportedPlatforms())
            {
                switch (supportedPlatform)
                {
                    case ESupportedPlatform.Windows:
                        windowsIcon.Visible = true;
                        break;
                    case ESupportedPlatform.Linux:
                        linuxIcon.Visible = true;
                        break;
                    case ESupportedPlatform.MacOs:
                        macOsIcon.Visible = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var platformRow = ActionRow.New();
                platformRow.Title = $"Download for {supportedPlatform.ToString()}";
                platformRow.Subtitle = $"{downloadInformation.GetDownloadsForPlatform(supportedPlatform).Count} file(s)";
                platformRow.Activatable = true;
                platformRow.Halign = Align.Fill;

                platformRow.OnActivated += (_, _) =>
                {
                    popover.Popdown();
                    HandleStartDownloads(downloadInformation, supportedPlatform);
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

        mainPopoverBox.Append(listBox);
        popover.Child = mainPopoverBox;
        _installButton.Popover = popover;
    }

    private async void HandleStartDownloads(DownloadInformation downloadInformation, ESupportedPlatform platform)
    {
        // Fetching the list of files:
        var fileList = downloadInformation.GetDownloadsForPlatform(platform);

        if (fileList.Count == 0)
            return;

        var fileCountStr = $"{fileList.Count} file";

        if (fileList.Count > 1)
        {
            fileCountStr += "s";
        }
        
        // Queueing the download of all files:
        foreach (var fileInformation in fileList)
        {
            try
            {
                var finalUrl = await GogServices.ResolveFinalDownloadUrlAsync(fileInformation.ManualUrl);
                var uri = new Uri(finalUrl);
                var fileName = Path.GetFileName(uri.AbsolutePath);
                
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"{_gameDetails.Title?.Replace(" ", "_")}_{platform.ToString()}.bin";
                }
            
                await Console.Out.WriteLineAsync($"[INFO] - Manual URL: {fileInformation.ManualUrl}");
                await Console.Out.WriteLineAsync($"[INFO] - Download URL: {finalUrl}");

                await DownloadQueueService.Instance.EnqueueDownload(
                    title: $"{fileInformation.FileName} ({fileInformation.Size})",
                    _gameDetails.Slug ?? string.Empty,
                    url: finalUrl,
                    targetFileName: fileName
                );
                
                _installButton.IconName = "object-select-symbolic";
                _installButton.Sensitive = false;
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"[ERROR] - Failed to queue the requested download: {e.Message}");
                _installButton.Sensitive = true;
                _installButton.Label = "Install";
            }
        }
        
        _parentWindow.ShowToastNotification($"Queued '{_gameDetails.Title}', in {downloadInformation.Language}, for {platform.ToString()} [{fileCountStr}]");
    }
    
    private static string CleanHtmlDescription(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) 
            return string.Empty;

        var cleaned = html.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = RegexHtmlBr().Replace(cleaned, Environment.NewLine);
        cleaned = RegexHtmlLi().Replace(cleaned, $"{Environment.NewLine}• ");
        cleaned = RegexHtmlHeadersParagraphs().Replace(cleaned, Environment.NewLine);
        cleaned = RegexHtmlTags().Replace(cleaned, string.Empty);
        cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
        cleaned = RegexTrimLines().Replace(cleaned, Environment.NewLine);
        cleaned = RegexHtmlExcessiveWhiteSpace().Replace(cleaned, string.Empty);

        return cleaned.Trim();
    }
    
    // Properties:
    public string Identifier => "GameDetailsView";
    public Widget RootWidget => _rootOverlay;

    // Some pre-compiled regexes, for performance:
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlBr();
    [GeneratedRegex("<li[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlLi();
    [GeneratedRegex("</p>|</h1>|</h2>|</h3>|</h4>|</ul>|</ol>|</div>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlHeadersParagraphs();
    [GeneratedRegex("<.*?>")] private static partial Regex RegexHtmlTags();
    [GeneratedRegex(@"[ \t\u00A0]*\n[ \t\u00A0]*")] private static partial Regex RegexTrimLines();
    [GeneratedRegex(@"\n{2,}")] private static partial Regex RegexHtmlExcessiveWhiteSpace();
}