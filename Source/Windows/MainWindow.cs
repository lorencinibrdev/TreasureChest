using Adw;
using GObject;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;
using TreasureChest.Source.Views;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;
using HeaderBar = Adw.HeaderBar;
using Value = GObject.Value;

namespace TreasureChest.Source.Windows;

[Subclass<ApplicationWindow>]
public partial class MainWindow
{
    // Private Members:
    private ViewStack _viewStack = null!;
    
    public static MainWindow New(Application app)
    {
        var window = NewWithProperties([]);

        window.Application = app;
        window.Title = "Treasure Chest";
        window.SetDefaultSize(ConfigurationService.Current.WindowWidth, ConfigurationService.Current.WindowHeight);

        if (ConfigurationService.Current.IsFirstRun)
        {
            Console.Out.WriteLine("[INFO] - First run detected.");
            window.SetState(EMainWindowViews.Welcome);
        }
        else if (!ConfigurationService.Current.IsLoggedIn)
        {
            Console.Out.WriteLine("[INFO] - Not the first run, but logged out.");
            window.SetState(EMainWindowViews.Login);
        }
        else
        {
            Console.Out.WriteLine("[INFO] - User previously logged in. Going straight to library view...");
            window.SetState(EMainWindowViews.Library);
            _ = window.ValidateSessionAsync();
        }
        
        return window;
    }

    partial void Initialize()
    {
        var headerBar = CreateHeaderBar();
        _viewStack = ViewStack.New();
        
        // Create factory views with navigation callbacks
        var firstRunPage = FirstRunView.Create(onGetStarted: () => SetState(EMainWindowViews.Login));
        var loginPage = LoginView.Create(this);
        var libraryPage = LibraryView.Create(this);

        _viewStack.AddNamed(firstRunPage, nameof(EMainWindowViews.Welcome));
        _viewStack.AddNamed(loginPage, nameof(EMainWindowViews.Login));
        _viewStack.AddNamed(libraryPage, nameof(EMainWindowViews.Library));

        var mainBox = Box.New(Orientation.Vertical, 0);
        mainBox.Append(headerBar);
        
        _viewStack.Vexpand = true;
        mainBox.Append(_viewStack);

        SetContent(mainBox);
    }

    private HeaderBar CreateHeaderBar()
    {
        var headerBar = HeaderBar.New();
        
        // Adding the buttons:
        var menuButton = MenuButton.New();
        menuButton.SetIconName("open-menu-symbolic");
        menuButton.SetTooltipText("Menu");
        menuButton.SetProperty("primary", new Value(true));

        var searchButton = Button.NewFromIconName("edit-find-symbolic");
        menuButton.SetTooltipText("Search");
        
        headerBar.PackEnd(menuButton);
        headerBar.PackEnd(searchButton);

        return headerBar;
    }

    private async Task ValidateSessionAsync()
    {
        var isSessionValid = await GogServices.RestoreSessionAsync();

        if (!isSessionValid)
        {
            SetState(EMainWindowViews.Login);
        }
        else
        {
            await Console.Out.WriteLineAsync("[INFO] - Session valid and correctly restored.");
            await LibraryView.RefreshNetworkLibraryDataAsync();
            await LibraryView.MergeAndUpdateLibrary(LibraryServices.GetLoadedGames());
        }
    }

    public void SetState(EMainWindowViews targetView)
    {
        _viewStack.SetVisibleChildName(targetView.ToString());
    }
    
    public void HandleApplicationExit(Gio.Application sender, EventArgs args)
    {
        // Saving the configuration back to the service:
        ConfigurationService.Current.WindowWidth = DefaultWidth;
        ConfigurationService.Current.WindowHeight = DefaultHeight;
    }
}