using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
using TreasureChest.Source.Windows;
using File = System.IO.File;
using Task = System.Threading.Tasks.Task;

namespace TreasureChest.Source.Views;

public class LibraryView
{
    // Private Members:
    private static MainWindow _parentWindow = null!;
    private static Gio.ListStore _store = null!;
    private static Stack _mainStack = null!;
    private static Stack _viewTypeStack = null!;
    private static FilterListModel _filterModel = null!;
    private static SortListModel _sortModel = null!;
    private static CustomSorter _sorter = null!;
    private static CustomFilter _filter = null!;
    private static bool _sortAscending = true;
    private static bool _showOnlyInstalled;
    
    public static Widget Create(MainWindow parentWindow)
    {
        _parentWindow = parentWindow;
        _store = Gio.ListStore.New(GameModel.GetGType());

        _filter = CustomFilter.New(item =>
        {
            var game = (GameModel)item;
            return !_showOnlyInstalled || game.IsInstalled;
        });
        
        _filterModel = FilterListModel.New(_store, _filter);

        _sorter = CustomSorter.New<GameModel>((a, b) =>
        {
            var comparison = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            return _sortAscending ? comparison : -comparison;
        });

        _sortModel = SortListModel.New(_filterModel, _sorter);

        var selectionModel = SingleSelection.New(_sortModel);
        selectionModel.Autoselect = false;

        // Setting up the Views:
        var gridView = CreateGridView(selectionModel);
        var gridScrolled = ScrolledWindow.New();
        gridScrolled.SetChild(gridView);
        gridScrolled.HscrollbarPolicy = PolicyType.Never;
        gridScrolled.Vexpand = true;
        gridScrolled.Hexpand = true;
        
        var listView = CreateListView(selectionModel);
        var listScrolled = ScrolledWindow.New();
        listScrolled.SetChild(listView);
        listScrolled.HscrollbarPolicy = PolicyType.Never;
        listScrolled.Vexpand = true;
        listScrolled.Hexpand = true;
        
        _viewTypeStack = Stack.New();
        _viewTypeStack.TransitionType = StackTransitionType.Crossfade;
        _viewTypeStack.Vexpand = true;
        _viewTypeStack.Hexpand = true;
        
        _viewTypeStack.AddNamed(gridScrolled, "grid_view");
        _viewTypeStack.AddNamed(listScrolled, "list_view");
        _viewTypeStack.SetVisibleChildName("grid_view");
        
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
        _mainStack.Vexpand = true;
        _mainStack.Hexpand = true;
        
        _mainStack.AddNamed(loadingBox, "loading_page");
        _mainStack.AddNamed(_viewTypeStack, "grid_page");
        _mainStack.SetVisibleChildName("loading_page");
        
        _ = LoadCachedLibraryDataAsync();
        
        return _mainStack;
    }
    
    private static void ShowGameDetailsModalDialog(GameModel game)
    {
        var fullGameDetails = LibraryServices.GetGameDetails(game.Id);

        if (fullGameDetails is null) 
            return;
        
        _parentWindow.ShowGameDetails(fullGameDetails);
    }

    public static Widget CreateViewSortControls()
    {
        var splitButton = Adw.SplitButton.New();
        splitButton.IconName = "view-list-symbolic";
        splitButton.TooltipText = "List View";
        splitButton.AddCssClass("flat");

        var isListView = false;
        splitButton.OnClicked += (sender, args) =>
        {
            isListView = !isListView;
            splitButton.IconName = isListView ? "view-grid-symbolic" : "view-list-symbolic";
            splitButton.TooltipText = isListView ? "Grid View" : "List View";
            SetViewMode(isListView);
        };

        var sortPopover = Popover.New();
        var popoverBox = Box.New(Orientation.Vertical, 6);
        popoverBox.MarginTop = 12;
        popoverBox.MarginBottom = 12;
        popoverBox.MarginStart = 12;
        popoverBox.MarginEnd = 12;

        var sortTitle = Label.New("Sort");
        sortTitle.AddCssClass("dim-label");
        sortTitle.Halign = Align.Start;

        var azRadio = CheckButton.NewWithLabel("A-Z");
        azRadio.Active = true;

        var zaRadio = CheckButton.NewWithLabel("Z-A");
        zaRadio.SetGroup(azRadio);

        azRadio.OnToggled += (_, _) =>
        {
            if (azRadio.Active)
            {
                SortLibrary(ascending: true);
            }
        };
        
        zaRadio.OnToggled += (_, _) =>
        {
            if (zaRadio.Active)
            {
                SortLibrary(ascending: false);
            }
        };

        var separator = Separator.New(Orientation.Horizontal);
        separator.MarginTop = 6;
        separator.MarginBottom = 6;

        var installedToggle = CheckButton.NewWithLabel("Show Installed Games Only");
        installedToggle.OnToggled += (_, _) =>
        {
            FilterInstalledGames(installedToggle.Active);
        };
        
        popoverBox.Append(sortTitle);
        popoverBox.Append(azRadio);
        popoverBox.Append(zaRadio);
        popoverBox.Append(separator);
        popoverBox.Append(installedToggle);
        
        sortPopover.SetChild(popoverBox);

        splitButton.Popover = sortPopover;
        return splitButton;
    }

    private static async Task LoadCachedLibraryDataAsync()
    {
        var cachedGames = await LibraryServices.LoadLibraryFromCacheAsync();

        if (cachedGames is not null && cachedGames.Count != 0)
        {
            await MergeAndUpdateLibrary(cachedGames);
            _ = ImagePrefetchService.PrefetchLibraryImagesAsync(cachedGames);
        }
    }

    public static async Task RefreshNetworkLibraryDataAsync()
    {
        if (!await GogServices.RefreshGameList())
            return;
        
        var liveGames = LibraryServices.GetLoadedGames();
        await MergeAndUpdateLibrary(liveGames);
        _ = ImagePrefetchService.PrefetchLibraryImagesAsync(liveGames);
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

            if (AddGame(gameDetail))
            {
                newEntriesFound++;    
            }
        }

        if (newEntriesFound > 0)
        {
            await Console.Out.WriteLineAsync($"[INFO] - Found {newEntriesFound} new entries. All of them merged.");
        }
        else
        {
            await Console.Out.WriteLineAsync("[INFO] - No new entries found. Already up to date.");
        }
        
        _mainStack.SetVisibleChildName("grid_page");
    }

    private static bool AddGame(GameDetailsResponse gameDetails)
    {
        // Skipping the DLC's and other downloadable contents:
        if (string.IsNullOrWhiteSpace(gameDetails.GameType) || gameDetails.GameType != "game")
            return false;
        
        var model = GameModel.New(gameDetails);
        _store.Append(model);
        return true;
    }

    private static void SortLibrary(bool ascending)
    {
        Console.WriteLine(ascending ? "[INFO] - Sorting A-Z" : "[INFO] - Sorting Z-A");
        _sortAscending = ascending;
        _sorter.Changed(SorterChange.Different);
    }
    
    private static void FilterInstalledGames(bool showOnlyInstalled)
    {
        Console.WriteLine(showOnlyInstalled ? "[INFO] - Hiding Uninstalled games" : "[INFO] - Showing all games");
        _showOnlyInstalled = showOnlyInstalled;
        _filter.Changed(FilterChange.Different);
    }

    private static void SetViewMode(bool showList)
    {
        Console.WriteLine(showList ? "[INFO] - Changing to ListView" : "[INFO] - Changing to GridView");
        _viewTypeStack.SetVisibleChildName(showList ? "list_view" : "grid_view");
    }

    private static GridView CreateGridView(SelectionModel selectionModel)
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
            var clickModel = (GameModel)_sortModel.GetObject(args.Position)!;
            ShowGameDetailsModalDialog(clickModel);
        };

        return gridView;
    }
    
    private static async void OnGridFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;

        // This is the main box for the view:
        var mainBox = Box.New(Orientation.Vertical, 8);
        mainBox.MarginTop = 16;
        mainBox.MarginBottom = 16;
        mainBox.MarginStart = 16;
        mainBox.MarginEnd = 16;
        
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
        
        // Preparing the icons for the supported platforms:
        var icons = LoadPlatformIcons();
        
        var platformBox = Box.New(Orientation.Horizontal, 6);
        platformBox.Halign = Align.Center;
        platformBox.Append(icons.WindowsIcon);
        platformBox.Append(icons.LinuxIcon);
        platformBox.Append(icons.MacOsIcon);
        
        mainBox.Append(imageStack);
        mainBox.Append(label);
        mainBox.Append(platformBox);
        
        listItem.Child = mainBox;
    }
    
    private static async void OnGridFactoryBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = (ListItem)args.Object;
        var game = (GameModel)listItem.Item!;
        var mainBox = (Box)listItem.Child!;
        
        // Fetching the widgets from the layout hierarchy:
        var imageStack = (Stack)mainBox.GetFirstChild()!;
        var label = (Label)imageStack.GetNextSibling()!;
        var platformBox = (Box)label.GetNextSibling()!;

        var spinner = (Spinner)imageStack.GetFirstChild()!;
        var picture = (Picture)spinner.GetNextSibling()!;
        
        // Preparing the icons:
        var windowsIcon = (Image)platformBox.GetFirstChild()!;
        var linuxIcon = (Image)windowsIcon.GetNextSibling()!;
        var macosIcon = (Image)linuxIcon.GetNextSibling()!;

        windowsIcon.Visible = game.SupportsWindows;
        linuxIcon.Visible = game.SupportsLinux;
        macosIcon.Visible = game.SupportsMacOs;
        
        label.SetText(game.Title);
        imageStack.SetVisibleChildName("spinner_page");
        spinner.Start();

        picture.SetFile(null);
        
        if (!game.IsInstalled)
        {
            picture.AddCssClass("grayscale");
        }

        var localImagePath = await ImagePrefetchService.FetchImageWithPriorityAsync(game.Id.ToString(), EImageType.Logo2X, game.LogoPath);

        if (listItem.Item != game) 
            return;
        
        if (File.Exists(localImagePath))
        {
            picture.SetFilename(localImagePath);
        }
            
        imageStack.SetVisibleChildName("image_page");
        spinner.Stop();
    }

    private static ListView CreateListView(SelectionModel selectionModel)
    {
        var listFactory = SignalListItemFactory.New();
        listFactory.OnSetup += OnListFactorySetup;
        listFactory.OnBind += OnListFactoryBind;

        var listView = ListView.New(selectionModel, listFactory);
        
        listView.OnActivate += (_, args) =>
        {
            var clickModel = (GameModel)_sortModel.GetObject(args.Position)!;
            ShowGameDetailsModalDialog(clickModel);
        };

        return listView;
    }
    
    private static void OnListFactorySetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;

        var rowBox = Box.New(Orientation.Horizontal, 12);
        rowBox.MarginTop = 6;
        rowBox.MarginBottom = 6;
        rowBox.MarginStart = 12;
        rowBox.MarginEnd = 12;

        var picture = Picture.New();
        picture.SetSizeRequest(48, 48);
        picture.ContentFit = ContentFit.Cover;

        var label = Label.New(null);
        label.Halign = Align.Start;
        label.Hexpand = true;
        label.Ellipsize = Pango.EllipsizeMode.End;
        
        // Preparing the icons for the supported platforms:
        var icons = LoadPlatformIcons();
        
        var platformBox = Box.New(Orientation.Horizontal, 6);
        platformBox.Halign = Align.End;
        platformBox.Append(icons.WindowsIcon);
        platformBox.Append(icons.LinuxIcon);
        platformBox.Append(icons.MacOsIcon);
        
        rowBox.Append(picture);
        rowBox.Append(label);
        rowBox.Append(platformBox);

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

        windowsIcon.Visible = game.SupportsWindows;
        linuxIcon.Visible = game.SupportsLinux;
        macosIcon.Visible = game.SupportsMacOs;

        label.SetText(game.Title);
        picture.SetFile(null);

        if (!game.IsInstalled)
        {
            picture.AddCssClass("grayscale");
        }

        var localImagePath = await ImagePrefetchService.FetchImageWithPriorityAsync(game.Id.ToString(), EImageType.SidebarIcon2X, game.IconPath);

        if (listItem.Item == game && File.Exists(localImagePath))
        {
            picture.SetFilename(localImagePath);
        }
    }

    private static (Image WindowsIcon, Image LinuxIcon, Image MacOsIcon) LoadPlatformIcons()
    {
        var windowsIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/windows-symbolic.svg");
        var linuxIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/linux-symbolic.svg");
        var macosIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/macos-symbolic.svg");

        windowsIcon.PixelSize = 16;
        linuxIcon.PixelSize = 16;
        macosIcon.PixelSize = 16;
        
        return (windowsIcon, linuxIcon, macosIcon);
    }
}