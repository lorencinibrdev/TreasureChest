using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public class GridCompactLibraryView : IView
{
    public GridCompactLibraryView(LibraryView parent, SingleSelection selectionModel, SortListModel sortModel)
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

        // This is the main box for the view:
        var mainBox = Box.New(Orientation.Vertical, 6);
        mainBox.Valign = Align.Start;
        mainBox.MarginTop = 6;
        mainBox.MarginBottom = 6;
        mainBox.MarginStart = 6;
        mainBox.MarginEnd = 6;
        
        var gameCover = Picture.New();
        gameCover.ContentFit = ContentFit.Cover;
        gameCover.SetSizeRequest(160, 90);

        var imageOverlay = Overlay.New();
        imageOverlay.Overflow = Overflow.Hidden;
        imageOverlay.AddCssClass("game-cover");
        imageOverlay.Child = gameCover;
        
        var badgeButton = Button.New();
        badgeButton.Valign = Align.Center;
        badgeButton.Halign = Align.Center;
        badgeButton.AddCssClass("osd");
        badgeButton.AddCssClass("circular");
        badgeButton.CanTarget = false;
        badgeButton.CanFocus = false;

        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(20, 20);
        spinner.Valign = Align.Center;
        spinner.Halign = Align.Center;
        spinner.CanTarget = false;
        spinner.CanFocus = false;
        spinner.Visible = false;

        imageOverlay.AddOverlay(badgeButton);
        imageOverlay.AddOverlay(spinner);
        
        var label = Label.New(null);
        label.Justify = Justification.Center;
        label.Lines = 1;
        label.Ellipsize = Pango.EllipsizeMode.End;
        label.AddCssClass("caption");
        
        mainBox.Append(imageOverlay);
        mainBox.Append(label);
        
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
            var picture = (Picture)imageOverlay.GetFirstChild()!;
            var badgeButton = (Button)picture.GetNextSibling()!;
            var downloadingSpinner = (Adw.Spinner)badgeButton.GetNextSibling()!;
        
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

            await LibraryItemHelper.BindImageAsync(listItem, gameModel, picture, EImageType.Logo, gameModel.LogoPath);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    private static void OnGridFactoryUnbind(SignalListItemFactory sender, SignalListItemFactory.UnbindSignalArgs args)
    {
        if (args.Object is not ListItem listItem) 
            return;
        
        LibraryItemHelper.UnbindState(listItem);
    }

    // Properties:
    public string Identifier => "GridCompactView";
    public Widget RootWidget { get; }
}