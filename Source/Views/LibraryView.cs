using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;

namespace TreasureChest.Source.Views;

public class LibraryView
{
    // Private Members:
    private static Gio.ListStore _store = null!;
    private static Stack _mainStack = null!;
    
    public static Widget Create(Window parentWindow)
    {
        _store = Gio.ListStore.New(GameModel.GetGType());

        var selectionModel = SingleSelection.New(_store);
        selectionModel.Autoselect = false;

        var gridFactory = SignalListItemFactory.New();
        gridFactory.OnSetup += OnGridFactorySetup;
        gridFactory.OnBind += OnGridFactoryBind;

        var gridView = GridView.New(selectionModel, gridFactory);
        gridView.MinColumns = 1;
        gridView.MaxColumns = 50;
        gridView.Halign = Align.Center;

        gridView.OnActivate += (sender, args) =>
        {
            var clickModel = (GameModel)_store.GetObject(args.Position)!;
            
            var fullGameDetails = LibraryServices.GetGameDetails(clickModel.Id);

            if (fullGameDetails is null) 
                return;
            
            var dialog = new GameDetailsDialog(parentWindow, fullGameDetails);
            dialog.Present();
        };
        
        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.SetChild(gridView);
        scrolledWindow.HscrollbarPolicy = PolicyType.Never;
        
        // Creating the stack, for the loading animation:
        var spinner = Spinner.New();
        spinner.Spinning = true;
        spinner.SetSizeRequest(64, 64);
        
        var loadingLabel = Label.New("Loading Library...");

        var loadingBox = Box.New(Orientation.Vertical, 16);
        loadingBox.Valign = Align.Center;
        loadingBox.Halign = Align.Center;
        loadingBox.Append(spinner);
        loadingBox.Append(loadingLabel);
        
        _mainStack = Stack.New();
        _mainStack.TransitionType = StackTransitionType.Crossfade;

        _mainStack.AddNamed(loadingBox, "loading_page");
        _mainStack.AddNamed(scrolledWindow, "grid_page");
        _mainStack.SetVisibleChildName("loading_page");
        
        _ = LoadCachedLibraryDataAsync();
        
        return _mainStack;
    }

    private static async Task LoadCachedLibraryDataAsync()
    {
        var cachedGames = await LibraryServices.LoadLibraryFromCacheAsync();

        if (cachedGames is not null && cachedGames.Count != 0)
        {
            PopulateStore(cachedGames);
            _mainStack.SetVisibleChildName("grid_page");
            _ = ImagePrefetchService.PrefetchLibraryImagesAsync(cachedGames);
        }
    }

    public static async Task RefreshNetworkLibraryDataAsync()
    {
        var sucess = await GogServices.RefreshGameList();

        if (sucess)
        {
            var liveGames = LibraryServices.GetLoadedGames();
            _store.RemoveAll();
            PopulateStore(liveGames);
            _mainStack.SetVisibleChildName("grid_page");
            _ = ImagePrefetchService.PrefetchLibraryImagesAsync(liveGames);
        }
    }

    public static async Task MergeAndUpdateLibrary(IReadOnlyList<GameDetailsResponse> gameDetails)
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetched a new game list from servers. Merging the diffs...");
        
        var existingIds = new HashSet<long>();
        var currentItemCount = _store.GetNItems();
        var newEntriesFound = 0;
        
        for (uint i = 0; i < currentItemCount; i++)
        {
            var model = (GameModel)_store.GetObject(i)!;
            existingIds.Add(model.Id);
        }

        foreach (var gameDetail in gameDetails)
        {
            if (!gameDetail.Id.HasValue || existingIds.Contains(gameDetail.Id.Value)) 
                continue;
            
            var newEntry = GameModel.New(gameDetail);
            _store.Append(newEntry);
            newEntriesFound++;
        }

        if (newEntriesFound > 0)
        {
            await Console.Out.WriteLineAsync($"[INFO] - Found {newEntriesFound} new entries. All of them merged.");
        }
        else
        {
            await Console.Out.WriteLineAsync($"[INFO] - No new entries found. Already up to date.");
        }
    }

    private static void PopulateStore(IReadOnlyList<GameDetailsResponse> games)
    {
        foreach (var game in games)
        {
            // Map your JSON response to the GObject wrapper required by GTK
            var model = GameModel.New(game);
            _store.Append(model);
        }
    }

    private static async void OnGridFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;

        // This is the main box for the view:
        var mainBox = Box.New(Orientation.Vertical, 8);
        mainBox.MarginBottom = 16;
        mainBox.MarginStart = 8;
        mainBox.MarginEnd = 8;
        
        // This is the stack for each individual game widget:
        var imageStack = Stack.New();
        imageStack.TransitionType = StackTransitionType.Crossfade;
        imageStack.SetSizeRequest(200, 120);
        imageStack.Vexpand = true;
        
        var spinner = Spinner.New();
        spinner.Valign = Align.Center;
        spinner.Halign = Align.Center;
        
        var picture = Picture.New();
        picture.Vexpand = true;
        picture.ContentFit = ContentFit.Contain;

        imageStack.AddNamed(spinner, "spinner_page");
        imageStack.AddNamed(picture, "image_page");
        
        var label = Label.New(null);
        label.Wrap = true;
        label.Justify = Justification.Center;
        
        mainBox.Append(imageStack);
        mainBox.Append(label);
        
        listItem.Child = mainBox;
    }
    
    private static async void OnGridFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = (ListItem)args.Object;
        var game = (GameModel)listItem.Item!;
        
        var mainBox = (Box)listItem.Child!;
        var imageStack = (Stack)mainBox.GetFirstChild()!;
        var label = (Label)mainBox.GetLastChild()!;

        var spinner = (Spinner)imageStack.GetFirstChild()!;
        var picture = (Picture)spinner.GetNextSibling()!;
        
        label.SetText(game.Title);
        imageStack.SetVisibleChildName("spinner_page");
        spinner.Start();

        picture.SetFile(null);
        
        var targetGame = game;

        var localImagePath = await ImagePrefetchService.FetchImageWithPriorityAsync(targetGame.Id.ToString(), EImageType.Logo2X, targetGame.LogoPath);
        
        if (listItem.Item == targetGame)
        {
            if (File.Exists(localImagePath))
            {
                picture.SetFilename(localImagePath);
            }
            
            imageStack.SetVisibleChildName("image_page");
            spinner.Stop();
        }
    }
}