using Adw;
using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;

namespace TreasureChest.Source.Views;

public class LoginView : IView
{
    // Private Members:
    private readonly StatusPage _statusPage;
    
    public LoginView(MainWindow parentWindow)
    {
        WebKit.Module.Initialize();
        
        _statusPage = StatusPage.New();
        _statusPage.Title = "Connect Your GOG Account";
        _statusPage.Description = "Sign in to sync your library and manage your games.";
        _statusPage.IconName = "avatar-default-symbolic";

        var button = Button.NewWithLabel("Log In to GOG");
        button.AddCssClass("suggested-action");
        button.AddCssClass("pill");
        button.OnClicked += async (_, _) =>
        {
            button.Sensitive = false;
            
            var loginModalDialog = new LoginDialog(parentWindow);
            var authCode = await loginModalDialog.ShowAndAuthenticateAsync();
            var loginSuccess = false;
            
            if (!string.IsNullOrEmpty(authCode))
            {
                loginSuccess = await GogServices.Login(authCode);
            }

            if (!loginSuccess)
            {
                button.Sensitive = true;
            }
            else
            {
                parentWindow.SetRootState(EMainWindowViews.MainApp);
            }
        };

        _statusPage.SetChild(button);
    }

    // Properties:
    public string Identifier => "LoginView";
    public Widget RootWidget => _statusPage;
}