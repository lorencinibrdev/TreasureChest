using Adw;
using GObject;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG;
using TreasureChest.Source.Services;
using TreasureChest.Source.Views;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;
using HeaderBar = Adw.HeaderBar;
using PreferencesWindow = TreasureChest.Source.Views.PreferencesWindow;
using Value = GObject.Value;

namespace TreasureChest.Source.Windows;

[Subclass<ApplicationWindow>]
public partial class MainWindow
{
    // Private Members:
    private static Application _parentApplication = null!;
    private Stack _rootStack = null!;
    private ViewStack _mainAppViewStack = null!;
    private HeaderBar _mainHeaderBar = null!;
    
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
        var libraryPage = LibraryView.Create(this);
        var userPage = Box.New(Orientation.Vertical, 0);
        
        var exploreViewPage = _mainAppViewStack.AddTitledWithIcon(explorePage, "explore", "Explore", "explore-symbolic");
        var libraryViewPage = _mainAppViewStack.AddTitledWithIcon(libraryPage, "library", "Library", "library-symbolic");
        var userViewPage = _mainAppViewStack.AddTitledWithIcon(userPage, "user", "User", "person-symbolic");
        
        // The header bar:
        _mainHeaderBar = CreateHeaderBar(_mainAppViewStack);
        
        var mainAppBox = Box.New(Orientation.Vertical, 0);
        mainAppBox.Append(_mainHeaderBar);
        mainAppBox.Append(_mainAppViewStack);
        
        // Create factory views with navigation callbacks
        var firstRunPage = FirstRunView.Create(onGetStarted: () => SetRootState(EMainWindowViews.Login));
        var loginPage = LoginView.Create(this);

        _rootStack.AddNamed(firstRunPage, nameof(EMainWindowViews.Welcome));
        _rootStack.AddNamed(loginPage, nameof(EMainWindowViews.Login));
        _rootStack.AddNamed(mainAppBox, nameof(EMainWindowViews.MainApp));
        
        SetContent(_rootStack);
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
            var preferencesWindow = new PreferencesWindow(this);
            preferencesWindow.Present();
        };
        
        AddAction(preferencesAction);
        
        _parentApplication.SetAccelsForAction("win.preferences", ["<Ctrl>comma"]);

        var librarySortAndViewControls = LibraryView.CreateViewSortControls();

        var searchButton = Button.NewFromIconName("edit-find-symbolic");
        menuButton.SetTooltipText("Search");
        
        headerBar.PackEnd(menuButton);
        headerBar.PackEnd(librarySortAndViewControls);
        headerBar.PackEnd(searchButton);

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
            await LibraryView.RefreshNetworkLibraryDataAsync();
            await LibraryView.MergeAndUpdateLibrary(LibraryServices.GetLoadedGames());
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
        var gameView = GameDetailsView.Create(this, gameDetails);
        
        _rootStack.AddNamed(gameView, gameId);
        _rootStack.SetVisibleChildName(gameId);
    }

    public async void NavigateBackToLibrary()
    {
        var activeGameView = _rootStack.GetVisibleChild();
        SetRootState(EMainWindowViews.MainApp);
        
        await Task.Delay(TransitionDurationExtended);

        if (activeGameView is null || activeGameView == _rootStack.GetVisibleChild()) 
            return;
        
        _rootStack.Remove(activeGameView);
        activeGameView.Dispose();
    }
    
    // Properties:
    private static int TransitionDuration => 300;
    private static int TransitionDurationExtended => TransitionDuration + 50;
}