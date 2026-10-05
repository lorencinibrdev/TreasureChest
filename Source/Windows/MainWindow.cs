using Adw;
using GObject;
using Gtk;
using TreasureChest.Source.Services;
using TreasureChest.Source.Views;
using Application = Adw.Application;
using ApplicationWindow = Adw.ApplicationWindow;
using HeaderBar = Adw.HeaderBar;

namespace TreasureChest.Source.Windows;

[Subclass<ApplicationWindow>]
public partial class MainWindow
{
    // Private Members:
    private ViewStack _viewStack = null!;
    
    public static MainWindow New(Application app, ApplicationConfiguration configData)
    {
        var window = NewWithProperties([]);

        window.Application = app;
        window.Title = "Treasure Chest";
        window.SetDefaultSize(configData.WindowWidth, configData.WindowHeight);

        if (configData.IsFirstRun)
        {
            Console.Out.WriteLine("[INFO] - First run detected.");
            window.SetState("first-run");
        }
        else
        {
            if (!configData.IsLoggedIn)
            {
                Console.Out.WriteLine("[INFO] - Not the first run, but logged out.");
                window.SetState("login");
            }
            else
            {
                Console.Out.WriteLine("[INFO] - Logged in.");
                window.SetState("library");
            }
        }
        
        return window;
    }

    partial void Initialize()
    {
        var headerBar = CreateHeaderBar();
        _viewStack = ViewStack.New();
        
        // Create factory views with navigation callbacks
        var firstRunPage = FirstRunView.Create(onGetStarted: () => SetState("login"));
        var loginPage = LoginView.Create(onLoginRequested: () => SetState("library"));
        var libraryPage = LibraryView.Create();

        _viewStack.AddNamed(firstRunPage, "first-run");
        _viewStack.AddNamed(loginPage, "login");
        _viewStack.AddNamed(libraryPage, "library");

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

    private void SetState(string pageName)
    {
        _viewStack.SetVisibleChildName(pageName);
    }
    
    public void HandleApplicationExit(Gio.Application sender, EventArgs args)
    {
        var configData = new ApplicationConfiguration
        {
            IsFirstRun = false,
            IsLoggedIn = false,
            WindowWidth = GetWidth(),
            WindowHeight = GetHeight()
        };
        
        ConfigurationService.SaveConfiguration(configData);
    }
}