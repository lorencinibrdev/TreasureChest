using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public class ListCompactLibraryView : IView
{
    public ListCompactLibraryView(LibraryView parent, SingleSelection selectionModel, SortListModel sortModel)
    {
        var listFactory = SignalListItemFactory.New();
        listFactory.OnSetup += (sender, args) => OnListFactorySetup(sender, args, parent);
        listFactory.OnBind += OnListFactoryBind;
        listFactory.OnUnbind += OnListFactoryUnbind;

        var listView = ListView.New(selectionModel, listFactory);
        listView.ShowSeparators = true;
        
        listView.OnActivate += (_, args) =>
        {
            if (sortModel.GetObject(args.Position) is GameModel clickedModel)
            {
                parent.ShowGameDetailsView(clickedModel);
            }
        };
        
        var listClamp = Adw.ClampScrollable.New();
        listClamp.MaximumSize = 960;
        listClamp.Child = listView;
        
        var listScrolled = ScrolledWindow.New();
        listScrolled.SetChild(listClamp);
        listScrolled.HscrollbarPolicy = PolicyType.Never;
        listScrolled.Vexpand = true;
        listScrolled.Hexpand = true;

        RootWidget = listScrolled;
    }
    
    private static void OnListFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args, LibraryView parent)
    {
        var listItem = (ListItem)args.Object;

        var rowBox = Box.New(Orientation.Horizontal, 10);
        rowBox.MarginTop = 4;
        rowBox.MarginBottom = 4;
        rowBox.MarginStart = 12;
        rowBox.MarginEnd = 12;

        var gameIcon = Picture.New();
        gameIcon.SetSizeRequest(32, 32);
        gameIcon.ContentFit = ContentFit.Cover;
        gameIcon.Overflow = Overflow.Hidden;
        gameIcon.AddCssClass("game-icon");

        var gameTitleLabel = Label.New(null);
        gameTitleLabel.Halign = Align.Start;
        gameTitleLabel.Hexpand = true;
        gameTitleLabel.Ellipsize = Pango.EllipsizeMode.End;

        var playTimeLabel = Label.New(null);
        playTimeLabel.AddCssClass("dim-label");
        playTimeLabel.AddCssClass("caption");
        playTimeLabel.AddCssClass("numeric");
        
        var (windowsIcon, linuxIcon, macOsIcon) = LibraryView.LoadPlatformIcons();
        
        var platformBox = Box.New(Orientation.Horizontal, 6);
        platformBox.Halign = Align.Center;
        platformBox.MarginStart = 12;
        platformBox.MarginEnd = 12;
        platformBox.Append(windowsIcon);
        platformBox.Append(linuxIcon);
        platformBox.Append(macOsIcon);
        
        var gameStateIcon = Image.New();
        gameStateIcon.MarginStart = 16;
        
        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(16, 16);
        
        rowBox.Append(gameIcon);
        rowBox.Append(gameTitleLabel);
        rowBox.Append(playTimeLabel);
        rowBox.Append(platformBox);
        rowBox.Append(gameStateIcon);
        rowBox.Append(spinner);

        listItem.Child = rowBox;
        GameContextMenuHelper.Attach(listItem, rowBox, parent);
    }

    private static async void OnListFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        try
        {
            var listItem = (ListItem)args.Object;
            
            if (listItem.Item is not GameModel gameModel || listItem.Child is not Box rowBox)
                return;
            
            var picture = (Picture)rowBox.GetFirstChild()!;
            var label = (Label)picture.GetNextSibling()!;
            var playtimeLabel = (Label)label.GetNextSibling()!;
            var platformBox = (Box)playtimeLabel.GetNextSibling()!;
            var gameStateIcon = (Image)platformBox.GetNextSibling()!;
            var downloadingSpinner = (Adw.Spinner)gameStateIcon.GetNextSibling()!;
            
            var windowsIcon = (Image)platformBox.GetFirstChild()!;
            var linuxIcon = (Image)windowsIcon.GetNextSibling()!;
            var macosIcon = (Image)linuxIcon.GetNextSibling()!;

            windowsIcon.Visible = gameModel.SystemCompatibility.Windows;
            linuxIcon.Visible = gameModel.SystemCompatibility.Linux;
            macosIcon.Visible = gameModel.SystemCompatibility.MacOs;

            label.SetText(gameModel.Title);

            var playStats = LibraryService.Instance.GetGamePlayStats(gameModel.Id);
            var formattedTime = LibraryItemHelper.FormatShowPlayTime(playStats.TimePlayed);
            playtimeLabel.SetText(formattedTime);
            playtimeLabel.Visible = !string.IsNullOrEmpty(formattedTime);
            
            var currentGameState = LibraryService.Instance.GetGameState(gameModel.Id);
            
            LibraryItemHelper.ApplyGameState(
                currentGameState,
                rowBox,
                picture,
                gameStateIcon,
                downloadingSpinner,
                icon => gameStateIcon.IconName = icon
            );

            await LibraryItemHelper.BindImageAsync(listItem, gameModel, picture, EImageType.SidebarIcon, gameModel.IconPath);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    private static void OnListFactoryUnbind(SignalListItemFactory sender, SignalListItemFactory.UnbindSignalArgs args)
    {
        if (args.Object is not ListItem listItem) 
            return;
        
        LibraryItemHelper.UnbindState(listItem);
    }
    
    // Properties:
    public string Identifier => "ListCompactView";
    public Widget RootWidget { get; }
}