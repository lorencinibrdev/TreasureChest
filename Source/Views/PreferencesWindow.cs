using Adw;
using Gtk;
using TreasureChest.Source.Services;
using Window = Gtk.Window;

namespace TreasureChest.Source.Views;

public class PreferencesWindow
{
    // Private Members:
    private readonly Adw.PreferencesWindow _window;

    public PreferencesWindow(Window parentWindow)
    {
        _window = Adw.PreferencesWindow.New();
        _window.TransientFor = parentWindow;
        _window.Title = "Preferences";
        _window.Modal = true;
        _window.DefaultWidth = 800;
        _window.DefaultHeight = 600;

        var page = PreferencesPage.New();
        page.IconName = "emblem-system-symbolic";
        page.Title = "General";

        var storageGroup = PreferencesGroup.New();
        storageGroup.Title = "Directories";
        storageGroup.Description = "The general directories used by the application for downloads and runtimes.";

        var libraryRow = CreatePathRow(
            title: "Games Installation Folder",
            subtitle: SettingsService.Instance.LibraryFolder,
            onSelectPath: path => SettingsService.Instance.LibraryFolder = path
        );
        
        var downloadsRow = CreatePathRow(
            title: "Installers Cache Folder",
            subtitle: SettingsService.Instance.DownloadsFolder,
            onSelectPath: path => SettingsService.Instance.DownloadsFolder = path
        );
        
        var runtimesRow = CreatePathRow(
            title: "Runtimes and Runners Folder",
            subtitle: SettingsService.Instance.RuntimesFolder,
            onSelectPath: path => SettingsService.Instance.RuntimesFolder = path
        );
        
        storageGroup.Add(libraryRow);
        storageGroup.Add(downloadsRow);
        storageGroup.Add(runtimesRow);
        
        var downloadsGroup = PreferencesGroup.New();
        downloadsGroup.Title = "Downloads";

        var autoCleanRow = SwitchRow.New();
        autoCleanRow.Title = "Auto-Clean Downloads";
        autoCleanRow.Subtitle = "Automatically clean installer files after a game was isntalled sucessfully.";
        autoCleanRow.Active = SettingsService.Instance.AutoCleanDownloads;

        autoCleanRow.OnNotify += (_, _) =>
        {
            SettingsService.Instance.AutoCleanDownloads = autoCleanRow.Active;
        };
        
        downloadsGroup.Add(autoCleanRow);
        
        page.Add(storageGroup);
        page.Add(downloadsGroup);

        _window.Add(page);
    }

    public void Present()
    {
        _window.Present();
    }

    private ActionRow CreatePathRow(string title, string subtitle, Action<string> onSelectPath)
    {
        var row = ActionRow.New();
        row.Title = title;
        row.Subtitle = string.IsNullOrWhiteSpace(subtitle) ? "Not Configured" : subtitle;

        var folderButton = Button.NewFromIconName("folder-open-symbolic");
        folderButton.Valign = Align.Center;
        folderButton.AddCssClass("flat");
        folderButton.TooltipText = "Select Folder";

        folderButton.OnClicked += async (_, _) =>
        {
            var dialog = FileDialog.New();
            dialog.Title = $"Select {title}";

            try
            {
                var folder = await dialog.SelectFolderAsync(_window);

                if (folder is null) 
                    return;
                
                var selectedPath = folder.GetPath();

                if (string.IsNullOrWhiteSpace(selectedPath)) 
                    return;
                    
                row.Subtitle = selectedPath;
                onSelectPath(selectedPath);
            }
            catch
            {
                // The dialog was canceled...
            }
        };
        
        row.AddSuffix(folderButton);
        row.ActivatableWidget = folderButton;
        return row;
    }
}