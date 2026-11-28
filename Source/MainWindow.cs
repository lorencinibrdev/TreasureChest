using Adw;
using GObject;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;
using TreasureChest.Source.Views;
using TreasureChest.Source.Views.Library;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;
using HeaderBar = Adw.HeaderBar;
using PreferencesDialog = TreasureChest.Source.Dialogs.PreferencesDialog;
using Spinner = Adw.Spinner;
using Value = GObject.Value;

namespace TreasureChest.Source;

[Subclass<ApplicationWindow>]
public partial class MainWindow
{
    // Private Members:
    private static Application _parentApplication = null!;
    private Stack _rootStack = null!;
    private ViewStack _mainAppViewStack = null!;
    private HeaderBar _mainHeaderBar = null!;
    private ToastOverlay _toastOverlay = null!;
    private Spinner _backgroundSyncSpinner = null!;
    
    // The views we have:
    private LibraryView _libraryView = null!;
    
    public static MainWindow New(Application parentApplication)
    {
        _parentApplication = parentApplication;
        
        var window = NewWithProperties([]);
        window.Application = parentApplication;
        window.Title = "Treasure Chest";
        window.SetDefaultSize(SettingsService.Instance.LastWindowWidth, SettingsService.Instance.LastWindowHeight);
        
        Console.Out.WriteLine($"[INFO] - Is User Logged In? {string.IsNullOrWhiteSpace(SettingsService.Instance.SavedUsername)} [{SettingsService.Instance.SavedUsername}]");

        if (string.IsNullOrWhiteSpace(SettingsService.Instance.SavedUsername))
        {
            window.SetRootState(EMainWindowViews.Welcome);
        }
        else
        {
            _ = window.EnterMainAppAsync(isFreshLogin: false);
        }
        
        return window;
    }

    partial void Initialize()
    {
        // The root stack:
        _rootStack = Stack.New();
        _rootStack.TransitionType = StackTransitionType.Crossfade;
        _rootStack.TransitionDuration = (uint)TransitionDuration;
        
        // The inner ViewStack (For the main window):
        _mainAppViewStack = ViewStack.New();
        _mainAppViewStack.Vexpand = true;
        
        // The tabbed pages:
        var explorePage = Box.New(Orientation.Vertical, 0);
        _libraryView = new LibraryView(this);
        var userPage = Box.New(Orientation.Vertical, 0);
        
        _mainAppViewStack.AddTitledWithIcon(explorePage, nameof(EMainWindowTabs.Explore).ToLowerInvariant(), nameof(EMainWindowTabs.Explore), "explore-symbolic");
        _mainAppViewStack.AddTitledWithIcon(_libraryView.RootWidget, nameof(EMainWindowTabs.Library).ToLowerInvariant(), nameof(EMainWindowTabs.Library), "library-symbolic");
        _mainAppViewStack.AddTitledWithIcon(userPage, nameof(EMainWindowTabs.User).ToLowerInvariant(), nameof(EMainWindowTabs.User), "person-symbolic");

        var targetView = SettingsService.Instance.StartupDefaultView.ToString().ToLowerInvariant();

        if (_mainAppViewStack.GetChildByName(targetView) is not null)
        {
            _mainAppViewStack.SetVisibleChildName(targetView);
        }
        
        // The header bar:
        _mainHeaderBar = CreateHeaderBar(_mainAppViewStack);
        
        var mainAppBox = Box.New(Orientation.Vertical, 0);
        mainAppBox.Append(_mainHeaderBar);
        mainAppBox.Append(_mainAppViewStack);
        
        // Create factory views with navigation callbacks
        var firstRunPage = new WelcomeView(this);

        _rootStack.AddNamed(firstRunPage.RootWidget, nameof(EMainWindowViews.Welcome));
        _rootStack.AddNamed(mainAppBox, nameof(EMainWindowViews.MainApp));
        
        // The overlay for showing toast notifications:
        _toastOverlay = ToastOverlay.New();
        _toastOverlay.Child = _rootStack;
        SetContent(_toastOverlay);
    }

    public async Task EnterMainAppAsync(bool isFreshLogin)
    {
        SetRootState(EMainWindowViews.MainApp);

        var isLibrary = _mainAppViewStack.VisibleChildName is not null &&
                        _mainAppViewStack.VisibleChildName.Equals(nameof(EMainWindowTabs.Library), StringComparison.InvariantCultureIgnoreCase);
        
        _libraryView.SetSearchActive(isLibrary);

        if (!isFreshLogin)
        {
            await LibraryService.Instance.LoadLibraryFromCacheAsync();

            if (!await GogService.Instance.RestoreSessionAsync())
            {
                SetRootState(EMainWindowViews.Welcome);
                ShowToastNotification("Session expired. Please log in again.");
                return;
            }
        }

        var hasCachedGames = !isFreshLogin && LibraryService.Instance.GamesCount > 0;
        _backgroundSyncSpinner.Visible = hasCachedGames;

        try
        {
            await GogService.Instance.RefreshGameList();
        }
        finally
        {
            _backgroundSyncSpinner.Visible = false;
        }
    }

    private HeaderBar CreateHeaderBar(ViewStack targetViewStack)
    {
        var headerBar = HeaderBar.New();
        
        // The tabs:
        var viewSwitcher = ViewSwitcher.New();
        viewSwitcher.Stack = targetViewStack;
        viewSwitcher.Policy = ViewSwitcherPolicy.Wide;
        headerBar.TitleWidget = viewSwitcher;
        
        // Adding the buttons:
        var menuButton = MenuButton.New();
        menuButton.SetIconName("open-menu-symbolic");
        menuButton.SetTooltipText("Main Menu");
        menuButton.SetProperty("primary", new Value(true));
        
        var menuModel = Gio.Menu.New();
        menuModel.Append("Preferences", "win.preferences");
        menuModel.Append("About", "win.about");
        menuButton.MenuModel = menuModel;
        
        var preferencesAction = Gio.SimpleAction.New("preferences", null);
        preferencesAction.OnActivate += (_, _) =>
        {
            var preferencesWindow = new PreferencesDialog(this);
            preferencesWindow.Present();
        };
        
        AddAction(preferencesAction);
        
        _parentApplication.SetAccelsForAction("win.preferences", ["<Ctrl>comma"]);
        
        var libraryControls = _libraryView.CreateHeaderControls();
        var isLibraryInitiallyVisible = targetViewStack.VisibleChildName is not null &&
                                        targetViewStack.VisibleChildName.Equals(nameof(EMainWindowTabs.Library), StringComparison.InvariantCultureIgnoreCase);
        
        libraryControls.Visible = isLibraryInitiallyVisible;
        _libraryView.SetSearchActive(isLibraryInitiallyVisible);

        targetViewStack.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "visible-child-name")
                return;
            
            var isLibrary = targetViewStack.VisibleChildName is not null &&
                            targetViewStack.VisibleChildName.Equals(nameof(EMainWindowTabs.Library), StringComparison.InvariantCultureIgnoreCase);
            libraryControls.Visible = isLibrary;
            _libraryView.SetSearchActive(isLibrary);
        };
        
        // The background fetch loading spinner:
        _backgroundSyncSpinner = Spinner.New();
        _backgroundSyncSpinner.TooltipText = "Synchronizing library with GOG servers...";
        _backgroundSyncSpinner.Visible = false;
        
        headerBar.PackStart(_backgroundSyncSpinner);
        headerBar.PackEnd(menuButton);
        headerBar.PackEnd(libraryControls);

        return headerBar;
    }
    
    public void HandleApplicationExit(Gio.Application sender, EventArgs args)
    {
        // Saving the configuration back to the service:
        SettingsService.Instance.LastWindowWidth = DefaultWidth;
        SettingsService.Instance.LastWindowHeight = DefaultHeight;
    }

    public void ShowGameDetails(GameEntry game)
    {
        _libraryView.SetSearchActive(false);
        
        var gameId = $"GameDetails_{game.Id}";
        var gameView = new GameDetailsView(this, game);
        
        _rootStack.AddNamed(gameView.RootWidget, gameId);
        _rootStack.SetVisibleChildName(gameId);
    }

    public void ShowToastNotification(string message)
    {
        var toast = Toast.New(message);
        toast.Timeout = 3;
        _toastOverlay.AddToast(toast);
    }
    
    public void ShowToastNotification(Toast toast)
    {
        _toastOverlay.AddToast(toast);
    }
    
    public async void NavigateBackToLibrary()
    {
        try
        {
            var activeGameView = _rootStack.GetVisibleChild();
            SetRootState(EMainWindowViews.MainApp);
            
            var isLibrary = _mainAppViewStack.VisibleChildName is not null &&
                            _mainAppViewStack.VisibleChildName.Equals(nameof(EMainWindowTabs.Library), StringComparison.InvariantCultureIgnoreCase);
            _libraryView.SetSearchActive(isLibrary);
            
            await Task.Delay(TransitionDurationExtended);

            if (activeGameView is null || activeGameView == _rootStack.GetVisibleChild()) 
                return;
        
            _rootStack.Remove(activeGameView);
            activeGameView.Dispose();
        }
        catch (Exception)
        {
            // Ignored
        }
    }
    
    private void SetRootState(EMainWindowViews targetView)
    {
        _rootStack.SetVisibleChildName(targetView.ToString());
    }
    
    // Properties:
    private static int TransitionDuration => 300;
    private static int TransitionDurationExtended => TransitionDuration + 50;
}