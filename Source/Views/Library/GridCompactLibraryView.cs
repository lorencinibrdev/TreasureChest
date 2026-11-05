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
        var mainBox = Box.New(Orientation.Vertical, 4);
        mainBox.Valign = Align.Start;
        mainBox.MarginTop = 8;
        mainBox.MarginBottom = 8;
        mainBox.MarginStart = 8;
        mainBox.MarginEnd = 8;
        
        var picture = Picture.New();
        picture.ContentFit = ContentFit.Cover;
        picture.SetSizeRequest(100, 60);

        var imageOverlay = Overlay.New();
        imageOverlay.Child = picture;
        
        var badgeButton = Button.New();
        badgeButton.Valign = Align.Center;
        badgeButton.Halign = Align.Center;
        badgeButton.AddCssClass("osd");
        badgeButton.AddCssClass("circular");
        badgeButton.CanTarget = false;
        badgeButton.CanFocus = false;

        var spinner = Adw.Spinner.New();
        spinner.SetSizeRequest(24, 24);
        spinner.Valign = Align.Center;
        spinner.Halign = Align.Center;
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
        
        mainBox.Append(imageOverlay);
        mainBox.Append(label);
        
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
            var picture = (Picture)imageOverlay.GetFirstChild()!;
            var badgeButton = (Button)picture.GetNextSibling()!;
            var downloadingSpinner = (Adw.Spinner)badgeButton.GetNextSibling()!;
        
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

            var localImagePath = await ImagePrefetchService.Instance.FetchImageWithPriorityAsync(game.Id, EImageType.Logo, game.LogoPath);

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
    public string Identifier => "GridCompactView";
    public Widget RootWidget { get; }
}