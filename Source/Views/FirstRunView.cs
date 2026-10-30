
using Adw;
using Gtk;

namespace TreasureChest.Source.Views;

public class FirstRunView : IView
{
    // Private Members:
    private readonly StatusPage _statusPage;

    public FirstRunView(Action onGetStarted)
    {
        _statusPage = StatusPage.New();
        _statusPage.Title = "Welcome to the application!";
        _statusPage.Description = "You one stop GOG download and installer!";
        _statusPage.IconName = "applications-games-symbolic";

        var button = Button.NewWithLabel("Get Started");
        button.AddCssClass("suggested-action");
        button.AddCssClass("pill");
        button.OnClicked += (_, _) => onGetStarted();
        
        _statusPage.SetChild(button);
    }

    // Properties:
    public string Identifier => "FirstRunView";
    public Widget RootWidget => _statusPage;
}