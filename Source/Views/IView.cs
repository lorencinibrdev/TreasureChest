using Gtk;

namespace TreasureChest.Source.Views;

public interface IView
{
    // Properties:
    public string Identifier { get; }
    public Widget RootWidget { get; }
}