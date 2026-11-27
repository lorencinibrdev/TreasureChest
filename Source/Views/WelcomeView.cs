using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using HeaderBar = Adw.HeaderBar;

namespace TreasureChest.Source.Views;

public class WelcomeView : IView
{
    // Private Members:
    private readonly ToolbarView _rootView;
    private readonly MainWindow? _parentWindow;

    public WelcomeView(MainWindow parentWindow)
    {
        _parentWindow = parentWindow;
        
        var headerBar = HeaderBar.New();
        headerBar.AddCssClass("flat");
        headerBar.ShowTitle = false;

        var featuresList = ListBox.New();
        featuresList.SelectionMode = SelectionMode.None;
        featuresList.AddCssClass("boxed-list");
        
        featuresList.Append(CreateFeatureRow(
            iconName: "view-refresh-symbolic",
            title: "Sync your library",
            subtitle: "Automatically keep your purchased GOG games up to date and in sync."
        ));
        
        featuresList.Append(CreateFeatureRow(
            iconName: "folder-download-symbolic",
            title: "Manage Installers & Extras",
            subtitle: "Download multi-part installers, DLCs, and bonus content."
        ));

        featuresList.Append(CreateFeatureRow(
            iconName: "media-playback-start-symbolic",
            title: "Play Natively on Linux",
            subtitle: "Launch games and track your playtime right from your library."
        ));

        var loginButton = Button.NewWithLabel("Login with GOG");
        loginButton.Halign = Align.Center;
        loginButton.MarginTop = 12;
        loginButton.AddCssClass("pill");
        loginButton.AddCssClass("suggested-action");
        loginButton.OnClicked += HandleLoginButtonClicked;
        
        var contentBox = Box.New(Orientation.Vertical, 24);
        contentBox.Append(featuresList);
        contentBox.Append(loginButton);

        var clamp = Clamp.New();
        clamp.MaximumSize = 460;
        clamp.Child = contentBox;

        var statusPage = StatusPage.New();
        statusPage.IconName = "io.github.lorencinibrdev.TreasureChest";
        statusPage.Title = "Welcome to Treasure Chest";
        statusPage.Description = "A native Linux client for browsing, downloading, and playing your GOG library.";
        statusPage.Child = clamp;
        
        _rootView = ToolbarView.New();
        _rootView.AddTopBar(headerBar);
        _rootView.Content = statusPage;
    }

    private async void HandleLoginButtonClicked(Button sender, EventArgs args)
    {
        try
        {
            if (_parentWindow is null)
                return;

            sender.Sensitive = false;

            var loginModalDialog = new LoginDialog(_parentWindow);
            var authCode = await loginModalDialog.ShowAndAuthenticateAsync();

            if (!string.IsNullOrEmpty(authCode) && await GogService.Instance.Login(authCode))
            {
                await _parentWindow.EnterMainAppAsync(isFreshLogin: true);
            }
        }
        catch (Exception)
        {
            // ignored
        }
        finally
        {
            sender.Sensitive = true;
        }
    }

    private static ActionRow CreateFeatureRow(string iconName, string title, string subtitle)
    {
        var row = ActionRow.New();
        row.Title = title.EscapeMarkup();
        row.Subtitle = subtitle.EscapeMarkup();
        row.Activatable = false;
        
        var icon = Image.NewFromIconName(iconName);
        icon.AddCssClass("accent");
        row.AddPrefix(icon);

        return row;
    }

    // Properties:
    public string Identifier => "FirstRunView";
    public Widget RootWidget => _rootView;
}