using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public class GridLibraryView : IView
{
    public GridLibraryView(LibraryView parent, SingleSelection selectionModel, SortListModel sortModel)
    {
        var gridFactory = SignalListItemFactory.New();
        gridFactory.OnSetup += OnGridFactorySetup;
        gridFactory.OnBind += OnGridFactoryBind;

        var gridView = GridView.New(selectionModel, gridFactory);
        gridView.MinColumns = 1;
        gridView.MaxColumns = 50;
        gridView.Halign = Align.Center;
        
        gridView.OnActivate += (_, args) =>
        {
            var clickModel = (GameModel)sortModel.GetObject(args.Position)!;
            parent.ShowGameDetailsView(clickModel);
        };

        var listScrolled = ScrolledWindow.New();
        listScrolled.SetChild(gridView);
        listScrolled.HscrollbarPolicy = PolicyType.Never;
        listScrolled.Vexpand = true;
        listScrolled.Hexpand = true;

        RootWidget = listScrolled;
    }

    private static void OnGridFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;

        // This is the main box for the view:
        var mainBox = Box.New(Orientation.Vertical, 8);
        mainBox.Valign = Align.Start;
        mainBox.MarginTop = 16;
        mainBox.MarginBottom = 16;
        mainBox.MarginStart = 16;
        mainBox.MarginEnd = 16;
        
        var picture = Picture.New();
        picture.ContentFit = ContentFit.Cover;
        picture.SetSizeRequest(200, 120);

        var imageOverlay = Overlay.New();
        imageOverlay.Child = picture;
        
        var badgeButton = Button.New();
        badgeButton.Valign = Align.Start;
        badgeButton.Halign = Align.End;
        badgeButton.MarginTop = 8;
        badgeButton.MarginEnd = 8;
        badgeButton.AddCssClass("osd");
        badgeButton.AddCssClass("circular");
        badgeButton.CanTarget = false;
        badgeButton.CanFocus = false;

        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(24, 24);
        spinner.Valign = Align.Start;
        spinner.Halign = Align.End;
        spinner.MarginTop = 8;
        spinner.MarginEnd = 8;
        spinner.CanTarget = false;
        spinner.CanFocus = false;
        spinner.Visible = false;

        imageOverlay.AddOverlay(badgeButton);
        imageOverlay.AddOverlay(spinner);
        
        var label = Label.New(null);
        label.Wrap = true;
        label.Justify = Justification.Center;
        label.Lines = 1;
        label.Ellipsize = Pango.EllipsizeMode.End;
        
        // Preparing the icons for the supported platforms:
        var (windowsIcon, linuxIcon, macOsIcon) = LibraryView.LoadPlatformIcons();
        
        var platformBox = Box.New(Orientation.Horizontal, 6);
        platformBox.Halign = Align.Center;
        platformBox.Append(windowsIcon);
        platformBox.Append(linuxIcon);
        platformBox.Append(macOsIcon);
        
        mainBox.Append(imageOverlay);
        mainBox.Append(label);
        mainBox.Append(platformBox);
        
        listItem.Child = mainBox;
    }
    
    private static async void OnGridFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        try
        {
            var listItem = (ListItem)args.Object;
            var game = (GameModel)listItem.Item!;
            var mainBox = (Box)listItem.Child!;
        
            // Fetching the widgets from the layout hierarchy:
            var imageOverlay = (Overlay)mainBox.GetFirstChild()!;
            var label = (Label)imageOverlay.GetNextSibling()!;
            var platformBox = (Box)label.GetNextSibling()!;
            var picture = (Picture)imageOverlay.GetFirstChild()!;
            var badgeButton = (Button)picture.GetNextSibling()!;
            var downloadingSpinner = (Adw.Spinner)badgeButton.GetNextSibling()!;
        
            // Preparing the icons:
            var windowsIcon = (Image)platformBox.GetFirstChild()!;
            var linuxIcon = (Image)windowsIcon.GetNextSibling()!;
            var macosIcon = (Image)linuxIcon.GetNextSibling()!;

            windowsIcon.Visible = game.SystemCompatibility.Windows;
            linuxIcon.Visible = game.SystemCompatibility.Linux;
            macosIcon.Visible = game.SystemCompatibility.MacOs;
        
            label.SetText(game.Title);
            picture.SetFile(null);

            var currentGameState = LibraryService.Instance.GetGameState(game.Id);

            switch (currentGameState)
            {
                case EGameState.Downloading:
                    picture.Opacity = 0.5;
                    badgeButton.Visible = false;
                    downloadingSpinner.Visible = true;
                    mainBox.TooltipText = "Downloading Game...";
                    break;
                case EGameState.Downloaded:
                    picture.Opacity = 0.8;
                    badgeButton.Visible = true;
                    badgeButton.IconName = "folder-download-symbolic";
                    downloadingSpinner.Visible = false;
                    mainBox.TooltipText = "Game is Downloaded, and ready to install.";
                    break;
                case EGameState.Installed:
                    picture.Opacity = 1.0;
                    badgeButton.Visible = false;
                    downloadingSpinner.Visible = false;
                    mainBox.TooltipText = "Game is Installed, and ready to play.";
                    break;
                case EGameState.Available:
                default:
                    picture.Opacity = 0.3;
                    badgeButton.Visible = true;
                    badgeButton.IconName = "cloud-symbolic";
                    downloadingSpinner.Visible = false;
                    mainBox.TooltipText = "Game is available for download.";
                    break;
            }

            var localImagePath = await ImagePrefetchService.Instance.FetchImageWithPriorityAsync(game.Id, EImageType.Logo2X, game.LogoPath);

            if (listItem.Item != game) 
                return;
        
            if (File.Exists(localImagePath))
            {
                picture.SetFilename(localImagePath);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    // Properties:
    public string Identifier => "GridView";
    public Widget RootWidget { get; }
}