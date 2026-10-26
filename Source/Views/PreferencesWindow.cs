using Adw;
using Gtk;
using TreasureChest.Source.Services;
using TreasureChest.Source.Services.Runners;
using Spinner = Gtk.Spinner;
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
        
        var generalPage = PrepareGeneralPage();
        var runnersPage = PrepareRunnersPage();
        
        _window.Add(generalPage);
        _window.Add(runnersPage);
    }

    public void Present()
    {
        _window.Present();
    }

    private PreferencesPage PrepareGeneralPage()
    {
        // The page itself:
        var page = PreferencesPage.New();
        page.IconName = "emblem-system-symbolic";
        page.Title = "General";

        // The storage group:
        var storageGroup = PreferencesGroup.New();
        storageGroup.Title = "Directories";
        storageGroup.Description = "The general directories used by the application for downloads and runtimes.";

        // Each setting row:
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
        
        storageGroup.Add(libraryRow);
        storageGroup.Add(downloadsRow);
        
        // The downloads group:
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
        
        // Adding the groups to the page, and returning:
        page.Add(storageGroup);
        page.Add(downloadsGroup);
        return page;
    }
    
    private PreferencesPage PrepareRunnersPage()
    {
        // The page itself:
        var page = PreferencesPage.New();
        page.IconName = "applications-system-symbolic";
        page.Title = "Runners";

        // The storage group:
        var storageGroup = PreferencesGroup.New();
        storageGroup.Title = "Directories";
        storageGroup.Description = "The directories used by the runtimes.";

        // Each setting row:
        var runtimesRow = CreatePathRow(
            title: "Runtimes and Runners Folder",
            subtitle: SettingsService.Instance.RuntimesFolder,
            onSelectPath: path => SettingsService.Instance.RuntimesFolder = path
        );
        
        storageGroup.Add(runtimesRow);
        
        // The runtimes group:
        var downloadsGroup = PreferencesGroup.New();
        downloadsGroup.Title = "Installed Runtimes";
        downloadsGroup.Description = "The engines used to run Windows, DOS and Arcade games in Linux";
        
        // Add runner rows dynamically
        downloadsGroup.Add(CreateRuntimeRow(
            runnerType: ERunnerType.DosBox, 
            displayName: "DOSBox Staging", 
            description: "DOS game emulator"
        ));

        downloadsGroup.Add(CreateRuntimeRow(
            runnerType: ERunnerType.ScummVm, 
            displayName: "ScummVM", 
            description: "Classic graphic adventure game engine"
        ));

        downloadsGroup.Add(CreateRuntimeRow(
            runnerType: ERunnerType.Wine, 
            displayName: "Wine / Proton", 
            description: "Windows compatibility layer"
        ));
        
        // Adding the groups to the page, and returning:
        page.Add(storageGroup);
        page.Add(downloadsGroup);
        return page;
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

    private ActionRow CreateRuntimeRow(ERunnerType runnerType, string displayName, string description)
    {
        var row = ActionRow.New();
        row.Title = displayName;
        
        var info = RunnerManagerService.GetRunnerInformation(runnerType);

        if (info.IsAvailable)
        {
            var locationTag = info.IsSystemInstalled ? "System" : "Portable";
            row.Subtitle = $"{description}\nPath: {info.ExecutablePath} ({locationTag})";

            var statusIcon = Image.NewFromIconName("emblem-ok-symbolic");
            statusIcon.Valign = Align.Center;
            statusIcon.TooltipText = "Installed and Ready";
            
            row.AddSuffix(statusIcon);
        }
        else
        {
            row.Subtitle = $"{description}\nStatus: Not Installed!";
            
            var downloadButton = Button.NewFromIconName("folder-download-symbolic");
            downloadButton.Valign = Align.Center;
            downloadButton.AddCssClass("flat");
            downloadButton.TooltipText = $"Download portable {displayName}";

            var spinner = Spinner.New();
            spinner.Valign = Align.Center;
            spinner.Visible = false;

            downloadButton.OnClicked += async (_, _) =>
            {
                downloadButton.Sensitive = false;
                downloadButton.Visible = false;
                spinner.Visible = true;
                spinner.Spinning = true;
                
                row.Subtitle = $"{description}\nStatus: Downloading portable runner...";

                var downloadUrl = GetDownloadUrlForRunner(runnerType);
                var success = await RunnerManagerService.Instance.InstallRunnerAsync(runnerType, downloadUrl);
                
                spinner.Spinning = false;
                spinner.Visible = false;

                if (success)
                {
                    var updatedInfo = RunnerManagerService.GetRunnerInformation(runnerType);
                    row.Subtitle = $"{description}\nPath: {updatedInfo.ExecutablePath} (Portable)";
                    
                    var checkIcon = Image.NewFromIconName("emblem-ok-symbolic");
                    checkIcon.Valign = Align.Center;

                    row.Remove(downloadButton);
                    row.Remove(spinner);
                    row.AddSuffix(checkIcon);
                }
                else
                {
                    downloadButton.Sensitive = true;
                    downloadButton.Visible = true;
                    row.Subtitle = $"{description}\nStatus: Download failed. Click to try again.";
                }
            };

            row.AddSuffix(spinner);
            row.AddSuffix(downloadButton);
            row.ActivatableWidget = downloadButton;
        }
        
        return row;
    }

    private string GetDownloadUrlForRunner(ERunnerType runnerType)
    {
        const string baseUrl = "https://github.com/lorencinibrdev/TreasureChest/releases/download/runtimes-v1";
        
        return runnerType switch
        {
            ERunnerType.DosBox => $"{baseUrl}/dosbox-staging-linux-x86_64-v0.83.0.tar.xz",
            ERunnerType.ScummVm => $"{baseUrl}/scummvm-2026.4.0-x86_64-linux.tar.xz",
            _ => throw new NotImplementedException($"No direct download URL configured for {runnerType}")
        };
    }
}