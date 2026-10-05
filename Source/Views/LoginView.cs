using Adw;
using Gtk;

namespace TreasureChest.Source.Views;

public class LoginView
{
    public static StatusPage Create(Action onLoginRequested)
    {
        var statusPage = StatusPage.New();
        statusPage.Title = "Connect Your GOG Account";
        statusPage.Description = "Sign in to sync your library and manage your games.";
        statusPage.IconName = "avatar-default-symbolic";

        var button = Button.NewWithLabel("Log In to GOG");
        button.AddCssClass("suggested-action");
        button.AddCssClass("pill");
        button.OnClicked += (_, _) => onLoginRequested();

        statusPage.SetChild(button);

        return statusPage;
    }
}