using Gtk;

namespace TreasureChest.Source.Views.Library;

public interface ILibraryViewMode
{
    // Properties:
    public string ViewName { get; }
    public Widget RootWidget { get; }
}