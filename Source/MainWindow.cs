using Adw;
using GObject;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
using TreasureChest.Source.Views;
using TreasureChest.Source.Widgets;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;
using HeaderBar = Adw.HeaderBar;
using PreferencesDialog = TreasureChest.Source.Dialogs.PreferencesDialog;
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
    
    // The views we have:
    private LibraryView _libraryView = null!;
    
    public static MainWindow New(Application parentApplication)
    {
        _parentApplication = parentApplication;
        
        var window = NewWithProperties([]);
        window.Application = parentApplication;
        window.Title = "Treasure Chest";
        window.SetDefaultSize(SettingsService.Instance.LastWindowWidth, SettingsService.Instance.LastWindowHeight);
        
        Console.Out.WriteLine($"[INFO] - Is First Run? {SettingsService.Instance.IsFirstRun}");
        Console.Out.WriteLine($"[INFO] - Is User Logged In? {string.IsNullOrWhiteSpace(SettingsService.Instance.SavedUsername)} [{SettingsService.Instance.SavedUsername}]");

        if (SettingsService.Instance.IsFirstRun)
        {
            window.SetRootState(EMainWindowViews.Welcome);
        }
        else if (string.IsNullOrWhiteSpace(SettingsService.Instance.SavedUsername))
        {
            window.SetRootState(EMainWindowViews.Login);
        }
        else
        {
            window.SetRootState(EMainWindowViews.MainApp);
            _ = window.ValidateSessionAsync();
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
        
        _mainAppViewStack.AddTitledWithIcon(explorePage, nameof(EMainWindowTabs.Explore).ToLower(), nameof(EMainWindowTabs.Explore), "explore-symbolic");
        _mainAppViewStack.AddTitledWithIcon(_libraryView.RootWidget, nameof(EMainWindowTabs.Library).ToLower(), nameof(EMainWindowTabs.Library), "library-symbolic");
        _mainAppViewStack.AddTitledWithIcon(userPage, nameof(EMainWindowTabs.User).ToLower(), nameof(EMainWindowTabs.User), "person-symbolic");

        var targetView = SettingsService.Instance.StartupDefaultView.ToString().ToLower();

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
        var firstRunPage = new FirstRunView(onGetStarted: () => SetRootState(EMainWindowViews.Login));
        var loginPage = new LoginView(this);

        _rootStack.AddNamed(firstRunPage.RootWidget, nameof(EMainWindowViews.Welcome));
        _rootStack.AddNamed(loginPage.RootWidget, nameof(EMainWindowViews.Login));
        _rootStack.AddNamed(mainAppBox, nameof(EMainWindowViews.MainApp));
        
        // The overlay for showing toast notifications:
        _toastOverlay = ToastOverlay.New();
        _toastOverlay.Child = _rootStack;
        SetContent(_toastOverlay);
    }

    public void SetRootState(EMainWindowViews targetView)
    {
        _rootStack.SetVisibleChildName(targetView.ToString());
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

        var librarySortAndViewControls = _libraryView.CreateViewSortControls();

        var searchButton = Button.NewFromIconName("edit-find-symbolic");
        menuButton.SetTooltipText("Search");
        
        // Adding the buttons:
        var downloadMenuButton = MenuButton.New();
        downloadMenuButton.SetIconName("open-menu-symbolic");
        downloadMenuButton.TooltipText = "Main Menu";
        _ = new DownloadsPopover(downloadMenuButton);
        
        headerBar.PackEnd(menuButton);
        headerBar.PackEnd(librarySortAndViewControls);
        headerBar.PackEnd(searchButton);
        headerBar.PackEnd(downloadMenuButton);

        return headerBar;
    }

    private async Task ValidateSessionAsync()
    {
        var isSessionValid = await GogServices.RestoreSessionAsync();

        if (!isSessionValid)
        {
            SetRootState(EMainWindowViews.Login);
        }
        else
        {
            await Console.Out.WriteLineAsync("[INFO] - Session valid and correctly restored.");
            await _libraryView.RefreshNetworkLibraryDataAsync();
            await _libraryView.MergeAndUpdateLibrary(LibraryServices.GetLoadedGames());
        }
    }
    
    public void HandleApplicationExit(Gio.Application sender, EventArgs args)
    {
        // Saving the configuration back to the service:
        SettingsService.Instance.LastWindowWidth = DefaultWidth;
        SettingsService.Instance.LastWindowHeight = DefaultHeight;
        SettingsService.Instance.IsFirstRun = false;
    }

    public void ShowGameDetails(GameDetailsResponse gameDetails)
    {
        var gameId = $"GameDetails_{gameDetails.Id}";
        var gameView = new GameDetailsView(this, gameDetails);
        
        _rootStack.AddNamed(gameView.RootWidget, gameId);
        _rootStack.SetVisibleChildName(gameId);
    }

    public void ShowToastNotification(string message)
    {
        var toast = Toast.New(message);
        toast.Timeout = 3;
        _toastOverlay.AddToast(toast);
    }
    
    public async void NavigateBackToLibrary()
    {
        try
        {
            var activeGameView = _rootStack.GetVisibleChild();
            SetRootState(EMainWindowViews.MainApp);
        
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
    
    // Properties:
    private static int TransitionDuration => 300;
    private static int TransitionDurationExtended => TransitionDuration + 50;
}