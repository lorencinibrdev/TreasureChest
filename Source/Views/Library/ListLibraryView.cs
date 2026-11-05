using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public class ListLibraryView : ILibraryViewMode
{
    public ListLibraryView(LibraryView parent, SingleSelection selectionModel, SortListModel sortModel)
    {
        var listFactory = SignalListItemFactory.New();
        listFactory.OnSetup += OnListFactorySetup;
        listFactory.OnBind += OnListFactoryBind;

        var listView = ListView.New(selectionModel, listFactory);
        
        listView.OnActivate += (_, args) =>
        {
            var clickModel = (GameModel)sortModel.GetObject(args.Position)!;
            parent.ShowGameDetailsView(clickModel);
        };

        var listClamp = Adw.ClampScrollable.New();
        listClamp.MaximumSize = 900;
        listClamp.Child = listView;
        
        var listScrolled = ScrolledWindow.New();
        listScrolled.SetChild(listClamp);
        listScrolled.HscrollbarPolicy = PolicyType.Never;
        listScrolled.Vexpand = true;
        listScrolled.Hexpand = true;

        RootWidget = listScrolled;
    }
    
    private void OnListFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;

        var rowBox = Box.New(Orientation.Horizontal, 12);
        rowBox.MarginTop = 6;
        rowBox.MarginBottom = 6;
        rowBox.MarginStart = 12;
        rowBox.MarginEnd = 12;

        var picture = Picture.New();
        picture.SetSizeRequest(62, 62);
        picture.ContentFit = ContentFit.Cover;

        var label = Label.New(null);
        label.Halign = Align.Start;
        label.Hexpand = true;
        label.Ellipsize = Pango.EllipsizeMode.End;
        
        var gameStateIcon = Image.New();
        gameStateIcon.MarginStart = 8;
        
        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(24, 24);
        spinner.MarginStart = 8;
        
        // Preparing the icons for the supported platforms:
        var (windowsIcon, linuxIcon, macOsIcon) = LibraryView.LoadPlatformIcons();
        
        var platformBox = Box.New(Orientation.Horizontal, 6);
        platformBox.Halign = Align.Center;
        platformBox.MarginStart = 16;
        platformBox.MarginEnd = 16;
        platformBox.Append(windowsIcon);
        platformBox.Append(linuxIcon);
        platformBox.Append(macOsIcon);
        
        rowBox.Append(picture);
        rowBox.Append(label);
        rowBox.Append(platformBox);
        rowBox.Append(gameStateIcon);
        rowBox.Append(spinner);

        listItem.Child = rowBox;
    }

    private static async void OnListFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = (ListItem)args.Object;
        var game = (GameModel)listItem.Item!;
        var rowBox = (Box)listItem.Child!;

        var picture = (Picture)rowBox.GetFirstChild()!;
        var label = (Label)picture.GetNextSibling()!;
        var platformBox = (Box)label.GetNextSibling()!;
        
        var windowsIcon = (Image)platformBox.GetFirstChild()!;
        var linuxIcon = (Image)windowsIcon.GetNextSibling()!;
        var macosIcon = (Image)linuxIcon.GetNextSibling()!;
        var gameStateIcon = (Image)platformBox.GetNextSibling()!;
        var downloadingSpinner = (Adw.Spinner)gameStateIcon.GetNextSibling()!;

        windowsIcon.Visible = game.SystemCompatibility.Windows;
        linuxIcon.Visible = game.SystemCompatibility.Linux;
        macosIcon.Visible = game.SystemCompatibility.MacOs;

        label.SetText(game.Title);
        picture.SetFile(null);

        var currentGameState = LibraryService.Instance.GetGameState(game.Id);

        currentGameState = EGameState.Downloading;

        switch (currentGameState)
        {
            case EGameState.Downloading:
                picture.Opacity = 0.5;
                gameStateIcon.Visible = false;
                downloadingSpinner.Visible = true;
                rowBox.TooltipText = "Downloading Game...";
                break;
            case EGameState.Downloaded:
                picture.Opacity = 0.8;
                gameStateIcon.Visible = true;
                gameStateIcon.IconName = "folder-download-symbolic";
                downloadingSpinner.Visible = false;
                rowBox.TooltipText = "Game is Downloaded, and ready to install.";
                break;
            case EGameState.Installed:
                picture.Opacity = 1.0;
                gameStateIcon.Visible = false;
                downloadingSpinner.Visible = false;
                rowBox.TooltipText = "Game is Installed, and ready to play.";
                break;
            case EGameState.Available:
            default:
                picture.Opacity = 0.3;
                gameStateIcon.Visible = true;
                gameStateIcon.IconName = "cloud-symbolic";
                downloadingSpinner.Visible = false;
                rowBox.TooltipText = "Game is available for download.";
                break;
        }

        var localImagePath = await ImagePrefetchService.Instance.FetchImageWithPriorityAsync(game.Id, EImageType.SidebarIcon2X, game.IconPath);

        if (listItem.Item == game && File.Exists(localImagePath))
        {
            picture.SetFilename(localImagePath);
        }
    }
    
    // Properties:
    public string ViewName => "ListView";
    public Widget RootWidget { get; }
}