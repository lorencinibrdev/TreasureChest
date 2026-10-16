using Adw;
using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services;
using TreasureChest.Source.Windows;

namespace TreasureChest.Source.Views;

public class LoginView
{
    public static StatusPage Create(MainWindow parentWindow)
    {
        WebKit.Module.Initialize();
        
        var statusPage = StatusPage.New();
        statusPage.Title = "Connect Your GOG Account";
        statusPage.Description = "Sign in to sync your library and manage your games.";
        statusPage.IconName = "avatar-default-symbolic";

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
                parentWindow.SetState(EMainWindowViews.Library);
            }
        };

        statusPage.SetChild(button);

        return statusPage;
    }
}