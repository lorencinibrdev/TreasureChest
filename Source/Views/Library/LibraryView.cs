using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Services.Library.Records;
using GameEntry = TreasureChest.Source.Services.Library.GameEntry;
using Task = System.Threading.Tasks.Task;

namespace TreasureChest.Source.Views.Library;

public class LibraryView : IView
{
    // Private Members:
    private readonly Gio.ListStore _store;
    private readonly Box _rootBox;
    private readonly Stack _mainStack;
    private readonly Stack _viewTypeStack;
    private readonly Dictionary<ELibraryViewMode, IView> _loadedViews = [];
    private readonly SingleSelection _selectionModel;
    private readonly SortListModel _sortModel;
    private readonly CustomSorter _sorter;
    private readonly CustomFilter _filter;
    private bool _sortAscending = true;
    private bool _showOnlyInstalled;
    private bool _initialLoadCompleted;
    
    // Search Fields:
    private readonly SearchBar _searchBar;
    private readonly SearchEntry _searchEntry;
    private string _searchQuery = string.Empty;

    public LibraryView(MainWindow parentWindow)
    {
        ParentWindow = parentWindow;
        _store = Gio.ListStore.New(GameModel.GetGType());

        _filter = CustomFilter.New(FilterGames);
        
        var filterModel = FilterListModel.New(_store, _filter);

        _sorter = CustomSorter.New<GameModel>((a, b) =>
        {
            var comparison = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            return _sortAscending ? comparison : -comparison;
        });

        _sortModel = SortListModel.New(filterModel, _sorter);

        _selectionModel = SingleSelection.New(_sortModel);
        _selectionModel.Autoselect = false;
        
        _viewTypeStack = Stack.New();
        _viewTypeStack.TransitionType = StackTransitionType.Crossfade;
        _viewTypeStack.Vexpand = true;
        _viewTypeStack.Hexpand = true;
        
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
        
        // The Search fields:
        _searchEntry = SearchEntry.New();
        _searchEntry.PlaceholderText = "Search games...";
        _searchEntry.Hexpand = true;

        _searchEntry.OnSearchChanged += (_, _) =>
        {
            var previousQuery = _searchQuery;
            _searchQuery = _searchEntry.GetText().Trim();

            var change = FilterChange.Different;

            if (!string.IsNullOrEmpty(previousQuery) && _searchQuery.Contains(previousQuery, StringComparison.OrdinalIgnoreCase))
            {
                change = FilterChange.MoreStrict;
            }
            else if (!string.IsNullOrEmpty(_searchQuery) && previousQuery.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
            {
                change = FilterChange.LessStrict;
            }
            
            _filter.Changed(change);

            if (_selectionModel.GetNItems() > 0)
            {
                _selectionModel.SelectItem(0, true);
            }
        };

        _searchEntry.OnActivate += (_, _) =>
        {
            if (_selectionModel.SelectedItem is GameModel targetGame)
            {
                ShowGameDetailsView(targetGame);
            }
        };

        var searchClamp = Adw.Clamp.New();
        searchClamp.MaximumSize = 400;
        searchClamp.Child = _searchEntry;

        _searchBar = SearchBar.New();
        _searchBar.Child = searchClamp;
        _searchBar.ConnectEntry(_searchEntry);
        _searchBar.SetKeyCaptureWidget(ParentWindow);
        
        LibraryService.Instance.LibraryUpdated += HandleLibraryUpdated;
        LibraryService.Instance.GameStateChanged += HandleGameStateChanged;
        LibraryService.Instance.GamePlayStatsChanged += HandlePlayTimeChanged;

        _rootBox = Box.New(Orientation.Vertical, 0);
        _rootBox.Append(_searchBar);
        _rootBox.Append(_mainStack);

        _rootBox.OnUnrealize += (_, _) =>
        {
            LibraryService.Instance.GameStateChanged -= HandleGameStateChanged;
            LibraryService.Instance.LibraryUpdated -= HandleLibraryUpdated;
        };
        
        // Setting up the current saved view:
        SetViewMode(SettingsService.Instance.ViewMode);
    }

    private void HandleLibraryUpdated()
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            var games = LibraryService.Instance.GetLoadedGames();
            _ = MergeAndUpdateStore(games);
            _ = ImagePrefetchService.Instance.PrefetchLibraryImagesAsync(games);
            return false;
        });
    }

    private bool FilterGames(GObject.Object item)
    {
        if (item is not GameModel gameModel)
            return false;

        if (_showOnlyInstalled && LibraryService.Instance.GetGameState(gameModel.Id) != EGameState.Installed)
            return false;

        if (!string.IsNullOrWhiteSpace(_searchQuery))
            return gameModel.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private void HandleGameStateChanged(long gameId, EGameState newState)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            for (uint i = 0; i < _store.GetNItems(); i++)
            {
                var model = (GameModel)_store.GetObject(i)!;

                if (model.Id != gameId) 
                    continue;

                model.NotifyStateChanged();
                
                break;
            }

            return false;
        });
    }
    
    private void HandlePlayTimeChanged(long gameId, GamePlayStats playTimeStats)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            for (uint i = 0; i < _store.GetNItems(); i++)
            {
                var model = (GameModel)_store.GetObject(i)!;

                if (model.Id != gameId) 
                    continue;

                model.UpdatePlayStats(playTimeStats);
                break;
            }

            return false;
        });
    }

    public void ShowGameDetailsView(GameModel game)
    {
        if (!LibraryService.Instance.TryGetGameEntry(game.Id, out var gameEntry))
            return;
        
        ParentWindow.ShowGameDetails(gameEntry);
    }

    public Widget CreateHeaderControls()
    {
        var controlsBox = Box.New(Orientation.Horizontal, 6);

        var searchButton = ToggleButton.New();
        searchButton.IconName = "system-search-symbolic";
        searchButton.TooltipText = "Search Library (Ctrl+F)";

        searchButton.BindProperty(
            "active",
            _searchBar,
            "search-mode-enabled",
            GObject.BindingFlags.Bidirectional | GObject.BindingFlags.SyncCreate
        );
        
        controlsBox.Append(searchButton);
        controlsBox.Append(CreateViewChangeButton());
        controlsBox.Append(CreateSortAndFilterButton());

        return controlsBox;
    }

    public void SetSearchActive(bool isTabActive)
    {
        _searchBar.SetKeyCaptureWidget(isTabActive ? ParentWindow : null);

        if (!isTabActive)
        {
            _searchBar.SearchModeEnabled = false;
        }
    }

    public Widget CreateViewChangeButton()
    {
        var iconBox = Box.New(Orientation.Horizontal, 0);
        iconBox.AddCssClass("linked");

        var currentMode = SettingsService.Instance.ViewMode;

        var viewButtonsGroup = new (Button button, ELibraryViewMode Mode)[]
        {
            (Button.NewFromIconName("view-grid-symbolic"), ELibraryViewMode.Grid),
            (Button.NewFromIconName("view-grid-compact-symbolic"), ELibraryViewMode.GridCompact),
            (Button.NewFromIconName("view-list-symbolic"), ELibraryViewMode.List),
            (Button.NewFromIconName("view-list-compact-symbolic"), ELibraryViewMode.ListCompact)
        };

        foreach (var (button, mode) in viewButtonsGroup)
        {
            if (mode == currentMode)
            {
                button.AddCssClass("suggested-action");
            }

            button.OnClicked += (sender, _) =>
            {
                foreach (var (b, m) in viewButtonsGroup)
                {
                    if (b == sender)
                    {
                        b.AddCssClass("suggested-action");
                        SetViewMode(m);
                    }
                    else
                    {
                        b.RemoveCssClass("suggested-action");
                    }
                }
            };
            
            iconBox.Append(button);
        }

        return iconBox;
    }
    
    public Widget CreateSortAndFilterButton()
    {
        // The sort button:
        var sortButton = MenuButton.New();
        sortButton.IconName = "view-sort-descending-symbolic";
        sortButton.TooltipText = "Sort and Filter";

        var sortPopover = Popover.New();
        var sortPopoverBox = Box.New(Orientation.Vertical, 6);
        sortPopoverBox.MarginTop = 12;
        sortPopoverBox.MarginBottom = 12;
        sortPopoverBox.MarginStart = 12;
        sortPopoverBox.MarginEnd = 12;

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
        
        sortPopoverBox.Append(sortTitle);
        sortPopoverBox.Append(azRadio);
        sortPopoverBox.Append(zaRadio);
        sortPopoverBox.Append(separator);
        sortPopoverBox.Append(installedToggle);
        
        sortPopover.SetChild(sortPopoverBox);
        sortButton.Popover = sortPopover;

        return sortButton;
    }

    public async Task MergeAndUpdateStore(IReadOnlyList<GameEntry> gameList)
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

        foreach (var game in gameList)
        {
            if (existingIds.Contains(game.Id)) 
                continue;

            if (AddGame(game))
            {
                newEntriesFound++;    
            }
        }

        if (_initialLoadCompleted && newEntriesFound > 0)
        {
            var message = newEntriesFound == 1
                ? "1 new game added to your library"
                : $"{newEntriesFound} new games added to your library";
            
            ParentWindow.ShowToastNotification(message);
            await Console.Out.WriteLineAsync(message);
        }
        else
        {
            await Console.Out.WriteLineAsync("[INFO] - No new entries found. Already up to date.");
        }

        _initialLoadCompleted = true;
        _mainStack.SetVisibleChildName("grid_page");
    }

    private bool AddGame(GameEntry game)
    {
        // Skipping the DLC's and other downloadable contents:
        if (game.Type != "game")
            return false;
        
        var model = GameModel.New(game);
        _store.Append(model);
        return true;
    }

    private void SortLibrary(bool ascending)
    {
        Console.WriteLine(ascending ? "[INFO] - Sorting A-Z" : "[INFO] - Sorting Z-A");
        _sortAscending = ascending;
        _sorter.Changed(SorterChange.Different);
    }
    
    private void FilterInstalledGames(bool showOnlyInstalled)
    {
        Console.WriteLine(showOnlyInstalled ? "[INFO] - Hiding Uninstalled games" : "[INFO] - Showing all games");
        _showOnlyInstalled = showOnlyInstalled;
        _filter.Changed(FilterChange.Different);
    }

    private void SetViewMode(ELibraryViewMode viewMode)
    {
        if (!_loadedViews.TryGetValue(viewMode, out var targetView))
        {
            // Loading the new view:
            targetView = viewMode switch
            {
                ELibraryViewMode.Grid => new GridLibraryView(this, _selectionModel, _sortModel),
                ELibraryViewMode.List => new ListLibraryView(this, _selectionModel, _sortModel),
                ELibraryViewMode.GridCompact => new GridCompactLibraryView(this, _selectionModel, _sortModel),
                ELibraryViewMode.ListCompact => new ListCompactLibraryView(this, _selectionModel, _sortModel),
                _ => throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, null)
            };
            
            _viewTypeStack.AddNamed(targetView.RootWidget, targetView.Identifier);
            _loadedViews.Add(viewMode, targetView);
        }
        
        SettingsService.Instance.ViewMode = viewMode;
        _viewTypeStack.SetVisibleChildName(targetView.Identifier);
        Console.WriteLine($"[INFO] - Changing LibraryViewMode: '{targetView.Identifier}' ({_loadedViews.Count} in cache)");
    }

    public static (Image WindowsIcon, Image LinuxIcon, Image MacOsIcon) LoadPlatformIcons()
    {
        var windowsIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/windows-symbolic.svg");
        windowsIcon.PixelSize = 16;
        windowsIcon.TooltipText = "Supports Windows";
        
        var linuxIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/linux-symbolic.svg");
        linuxIcon.PixelSize = 16;
        linuxIcon.TooltipText = "Supports Linux";
        
        var macosIcon = Image.NewFromResource("/io/github/lorencinibrdev/TreasureChest/icons/macos-symbolic.svg");
        macosIcon.PixelSize = 16;
        macosIcon.TooltipText = "Supports MacOS";
        
        return (windowsIcon, linuxIcon, macosIcon);
    }
    
    // Properties:
    public string Identifier => "LibraryView";
    public MainWindow ParentWindow { get; }
    public Widget RootWidget => _rootBox;
}