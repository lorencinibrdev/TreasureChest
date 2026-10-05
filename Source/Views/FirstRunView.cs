
using Adw;
using Gtk;

namespace TreasureChest.Source.Views;

public static class FirstRunView
{
    public static StatusPage Create(Action onGetStarted)
    {
        var statusPage = StatusPage.New();
        statusPage.Title = "Welcome to the application!";
        statusPage.Description = "You one stop GOG download and installer!";
        statusPage.IconName = "applications-games-symbolic";

        var button = Button.NewWithLabel("Get Started");
        button.AddCssClass("suggested-action");
        button.AddCssClass("pill");
        button.OnClicked += (_, _) => onGetStarted();
        
        statusPage.SetChild(button);

        return statusPage;
    }
}