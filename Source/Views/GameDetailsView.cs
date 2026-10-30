using System.Text.RegularExpressions;
using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
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
    private readonly Button _installButton;
    
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

        _installButton = Button.NewWithLabel("Install");
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
    
    private Dictionary<string, Dictionary<string, string>> ExtractDownloadsMatrix(GameContentResponse gameContent)
    {
        var matrix = new Dictionary<string, Dictionary<string, string>>();

        if (gameContent.Downloads is null)
            return matrix;

        foreach (var langGroup in gameContent.Downloads)
        {
            if (string.IsNullOrWhiteSpace(langGroup.Language) || !langGroup.Platforms.HasData)
                continue;
            
            var platformDict = new Dictionary<string, string>();
            
            var linuxLists = langGroup.Platforms.Linux?.FirstOrDefault()?.ManualUrl ?? null;

            if (linuxLists is not null)
            {
                platformDict["Linux"] = linuxLists;
            }
            
            var windowsLists = langGroup.Platforms.Windows?.FirstOrDefault()?.ManualUrl ?? null;

            if (windowsLists is not null)
            {
                platformDict["Windows"] = windowsLists;
            }
            
            var macLists = langGroup.Platforms.Mac?.FirstOrDefault()?.ManualUrl ?? null;

            if (macLists is not null)
            {
                platformDict["MacOS"] = macLists;
            }

            if (platformDict.Count > 0)
            {
                matrix[langGroup.Language] = platformDict;
            }
        }

        return matrix;
    }

    private void SetupInstallButton(GameContentResponse contentDetails)
    {
        _installButton.Label = "Install...";
        _installButton.Sensitive = true;
        
        var parsedDownloadsMatrix = ExtractDownloadsMatrix(contentDetails);

        _installButton.OnClicked += (_, _) =>
        {
            var setupDialog = new GameInstallSetupDialog(_parentWindow, _gameDetails.Title ?? "Unknown Game Title", parsedDownloadsMatrix);
            setupDialog.Present();
        };
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