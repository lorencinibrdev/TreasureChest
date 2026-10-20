using System.Text.RegularExpressions;
using Adw;
using Gtk;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
using Dialog = Adw.Dialog;
using HeaderBar = Adw.HeaderBar;
using Window = Gtk.Window;

namespace TreasureChest.Source.Views;

public partial class GameDetailsDialog
{
    // Private Members:
    private readonly Dialog _modalDialog;
    private readonly Window _parentWindow;
    private readonly PreferencesGroup _dlcsGroup;
    private readonly PreferencesGroup _extrasGroup;
    private readonly Label _descriptionLabel = null!;
    
    public GameDetailsDialog(Window parentWindow, GameDetailsResponse selectedGame)
    {
        _parentWindow = parentWindow;
        
        _modalDialog = Dialog.New();
        _modalDialog.ContentWidth = 900;
        _modalDialog.ContentHeight = 700;
        
        var rootOverlay = Overlay.New();
        
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
        var titleLabel = Label.New(selectedGame.Title);
        titleLabel.AddCssClass("title-1");
        titleLabel.Halign = Align.Start;
        titleLabel.Wrap = true;

        var playButton = Button.NewWithLabel("Install");
        playButton.AddCssClass("suggested-action");
        playButton.AddCssClass("pill");
        playButton.Halign = Align.Start;
        playButton.SetSizeRequest(140, -1);
        
        controlsBox.Append(titleLabel);
        controlsBox.Append(playButton);
        overlay.AddOverlay(controlsBox);
        scrollableBox.Append(overlay);
        
        // The content for downloads and such:
        var contentClamp = Clamp.New();
        contentClamp.MaximumSize = 750;
        contentClamp.MarginTop = 32;
        contentClamp.MarginBottom = 48;
        contentClamp.MarginStart = 24;
        contentClamp.MarginEnd = 24;

        _descriptionLabel = Label.New(string.Empty);
        _descriptionLabel.Wrap = true;
        _descriptionLabel.Xalign = 0;
        _descriptionLabel.MarginBottom = 32;
        _descriptionLabel.Visible = false;
        
        var contentBox = Box.New(Orientation.Vertical, 24);
        
        _dlcsGroup = PreferencesGroup.New();
        _dlcsGroup.Title = "DLC's";
        _dlcsGroup.Description = "Downloadable content for this game.";
        _dlcsGroup.Visible = false;
        
        _extrasGroup = PreferencesGroup.New();
        _extrasGroup.Title = "Extras";
        _extrasGroup.Description = "Arts, Music and other extras.";
        _extrasGroup.Visible = false;

        contentBox.Append(_descriptionLabel);
        contentBox.Append(_dlcsGroup);
        contentBox.Append(_extrasGroup);

        contentClamp.Child = contentBox;
        scrollableBox.Append(contentClamp);

        rootOverlay.Child = scrolledWindow;

        var headerBar = HeaderBar.New();
        headerBar.AddCssClass("flat");
        headerBar.Valign = Align.Start;
        rootOverlay.AddOverlay(headerBar);
        
        _modalDialog.Child = rootOverlay;
        
        _ = LoadImagesAsync(selectedGame, pictureBg);
        _ = FetchGameDetails(selectedGame);
    }

    public void Present()
    {
        _modalDialog.Present(_parentWindow);
    }

    private async Task LoadImagesAsync(GameDetailsResponse selectedGame, Picture bgPicture)
    {
        var localBgImagePath = await ImagePrefetchService.FetchImageWithPriorityAsync(selectedGame.Id.ToString()!, EImageType.Background, selectedGame.Images?.Background);

        if (File.Exists(localBgImagePath))
        {
            bgPicture.SetFilename(localBgImagePath);
        }
    }
    
    private async Task FetchGameDetails(GameDetailsResponse selectedGame)
    {
        if (await GogServices.FetchGameContentDetails(selectedGame) is not GameContentResponse gameContent || selectedGame.Id is null)
            return;
        
        // Handling the description:
        var productDescription = await GogServices.FetchGameDescriptionAsync(selectedGame.Id.Value);
        var leadDescription = productDescription?.Description?.Lead ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(leadDescription))
        {
            _descriptionLabel.SetText(CleanHtmlDescription(leadDescription));
            _descriptionLabel.Visible = true;
        }
        
        // Handling the DLCs:
        if (selectedGame.DlcInformation is not null && selectedGame.DlcInformation.HasDlc)
        {
            var dlcDetails = await GogServices.FetchGameDlcContentDetails(selectedGame);

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

        downloadButton.OnClicked += (sender, args) =>
        {
            Console.Out.WriteLine($"[INFO] - Starting download for: {title}");
        };

        row.AddSuffix(pillBox);
        row.AddSuffix(downloadButton);
        return row;
    }

    private string CleanHtmlDescription(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) 
            return string.Empty;

        var cleaned = System.Net.WebUtility.HtmlDecode(html);
        cleaned = RegexHtmlBr().Replace(cleaned, "\n");
        cleaned = RegexHtmlLi().Replace(cleaned, "• ");
        cleaned = RegexHtmlLiClose().Replace(cleaned, "\n");
        cleaned = RegexHtmlHeadersParagraphs().Replace(cleaned, "\n\n");
        cleaned = RegexHtmlTags().Replace(cleaned, string.Empty);
        cleaned = RegexHtmlExcessiveWhiteSpace().Replace(cleaned, "\n\n");
        return cleaned.Trim();
    }
    
    // Some pre-compiled Regexes, for speed:
    [GeneratedRegex(@"<br\s*/?>")] private static partial Regex RegexHtmlBr();
    [GeneratedRegex("<li[^>]*>")] private static partial Regex RegexHtmlLi();
    [GeneratedRegex("</li>")] private static partial Regex RegexHtmlLiClose();
    [GeneratedRegex("</p>|</h3>|</h1>|</h2>")] private static partial Regex RegexHtmlHeadersParagraphs();
    [GeneratedRegex("<.*?>")] private static partial Regex RegexHtmlTags();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex RegexHtmlExcessiveWhiteSpace();
}