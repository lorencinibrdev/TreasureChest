using System.Text.RegularExpressions;
using Adw;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;
using HeaderBar = Adw.HeaderBar;
using Spinner = Gtk.Spinner;

namespace TreasureChest.Source.Views;

public partial class GameDetailsView : IView
{
    // Private Members:
    private readonly MainWindow _parentWindow;
    private readonly Overlay _rootOverlay;
    private readonly PreferencesGroup _dlcGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly Label _descriptionLabel;
    private readonly MenuButton _installButton;
    
    private readonly GameEntry _game;
    private GameContentEntry? _content;

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
        try
        {
            if (await GogService.Instance.FetchGameContentDetails(_game) is not GogResponseGameContent gameContentResponse)
            {
                _installButton.Label = "Game Unavailable";
                _installButton.Sensitive = false;
                return;
            }
        
            // Saving the game content data:
            _content = GameContentEntry.TryCreate(gameContentResponse);

            if (_content is null)
            {
                _installButton.Label = "Game Unavailable";
                _installButton.Sensitive = false;
                return;
            }
            
            SetupInstallButton(_content);
        
            // Handling the description:
            var productDescription = await GogService.Instance.FetchGameDescriptionAsync(_game.Id);
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
            _dlcGroup.Visible = _content.Dlcs.Count > 0;

            foreach (var dlcContent in _content.Dlcs)
            {
                var languagesAvailable = string.Join(", ", dlcContent.Downloads.Keys);
                
                var row = CreateDownloadRow(dlcContent.Title, "DLC Content", languagesAvailable);
                _dlcGroup.Add(row);
            }

            // Handling the extras:
            _extrasGroup.Visible = _content.Extras.Count > 0;
            
            foreach (var extraContent in _content.Extras)
            {
                var row = CreateDownloadRow(extraContent.Name, extraContent.Type, extraContent.Size);
                _extrasGroup.Add(row);
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

    private void SetupInstallButton(GameContentEntry content)
    {
        _installButton.Sensitive = true;
        
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

        foreach (var (languageString, availablePlatforms) in content.Downloads)
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
                    popover.Popdown();
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

        mainPopoverBox.Append(listBox);
        popover.Child = mainPopoverBox;
        _installButton.Popover = popover;
    }

    private async void HandleStartDownloads(string language, PlatformContent platformContent)
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

                await DownloadQueueService.Instance.EnqueueDownload(
                    title: $"{downloadItem.Name} ({downloadItem.Size})",
                    _game.Slug,
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
        
        _parentWindow.ShowToastNotification($"Queued '{_game.Title}', in {language}, for {platformContent.Platform.ToString()} [{fileCountStr}]");
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