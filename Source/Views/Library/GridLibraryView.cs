using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public class GridLibraryView : IView
{
    // Private Members:
    public GridLibraryView(LibraryView parent, SingleSelection selectionModel, SortListModel sortModel)
    {
        var gridFactory = SignalListItemFactory.New();
        gridFactory.OnSetup += (sender, args) => OnGridFactorySetup(sender, args, parent);
        gridFactory.OnBind += OnGridFactoryBind;

        var gridView = GridView.New(selectionModel, gridFactory);
        gridView.MinColumns = 2;
        gridView.MaxColumns = 50;
        gridView.AddCssClass("library-grid");
        
        gridView.OnActivate += (_, args) =>
        {
            if (sortModel.GetObject(args.Position) is GameModel clickedModel)
            {
                parent.ShowGameDetailsView(clickedModel);
            }
        };

        var listScrolled = ScrolledWindow.New();
        listScrolled.SetChild(gridView);
        listScrolled.HscrollbarPolicy = PolicyType.Never;
        listScrolled.Vexpand = true;
        listScrolled.Hexpand = true;

        RootWidget = listScrolled;
    }

    private static void OnGridFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args, LibraryView parent)
    {
        var listItem = (ListItem)args.Object;

        var mainBox = Box.New(Orientation.Vertical, 8);
        mainBox.Valign = Align.Start;
        mainBox.MarginTop = 8;
        mainBox.MarginBottom = 8;
        mainBox.MarginStart = 8;
        mainBox.MarginEnd = 8;
        
        var gameCover = Picture.New();
        gameCover.ContentFit = ContentFit.Cover;
        gameCover.SetSizeRequest(240, 135);

        var imageOverlay = Overlay.New();
        imageOverlay.Overflow = Overflow.Hidden;
        imageOverlay.AddCssClass("game-cover");
        imageOverlay.Child = gameCover;
        
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
        GameContextMenuHelper.Attach(listItem, mainBox, parent);
    }
    
    private static async void OnGridFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        try
        {
            var listItem = (ListItem)args.Object;

            if (listItem.Item is not GameModel gameModel || listItem.Child is not Box mainBox)
                return;
        
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

            windowsIcon.Visible = gameModel.SystemCompatibility.Windows;
            linuxIcon.Visible = gameModel.SystemCompatibility.Linux;
            macosIcon.Visible = gameModel.SystemCompatibility.MacOs;
        
            label.SetText(gameModel.Title);

            var currentGameState = LibraryService.Instance.GetGameState(gameModel.Id);
            
            LibraryItemHelper.ApplyGameState(
                currentGameState,
                mainBox,
                picture,
                badgeButton,
                downloadingSpinner,
                icon => badgeButton.IconName = icon
            );

            await LibraryItemHelper.BindImageAsync(listItem, gameModel, picture, EImageType.Logo2X, gameModel.LogoPath);
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