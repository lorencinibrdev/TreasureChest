using Gtk;
using TreasureChest.Source.Dialogs;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Launcher;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public static class GameContextMenuHelper
{
    public static void Attach(ListItem listItem, Widget targetWidget, LibraryView libraryView)
    {
        var popoverMenu = PopoverMenu.NewFromModel(null);
        popoverMenu.SetParent(targetWidget);
        popoverMenu.HasArrow = false;
        popoverMenu.Halign = Align.Start;
        
        targetWidget.OnDestroy += (_, _) => popoverMenu.Unparent();

        var rightClick = GestureClick.New();
        rightClick.SetButton(3);
        
        rightClick.OnPressed += (sender, args) =>
        {
            if (listItem.Item is not GameModel gameModel)
                return;
         
            Console.Out.WriteLine($"[INFO] - Right-Click: '{gameModel.Title}'");
            
            sender.SetState(EventSequenceState.Claimed);

            var menu = BuildMenuForGame(targetWidget, gameModel, libraryView);
            popoverMenu.SetMenuModel(menu);
            popoverMenu.SetPointingTo(new Gdk.Rectangle
            {
                X = (int)args.X,
                Y = (int)args.Y,
                Width = 1,
                Height = 1,
            });
            
            popoverMenu.Popup();
        };
        
        targetWidget.AddController(rightClick);
    }

    private static Gio.Menu BuildMenuForGame(Widget targetWidget, GameModel gameModel, LibraryView libraryView)
    {
        var rootMenu = Gio.Menu.New();
        var actionGroup = Gio.SimpleActionGroup.New();
        var gameState = LibraryService.Instance.GetGameState(gameModel.Id);

        var primarySection = Gio.Menu.New();
        
        switch (gameState)
        {
            case EGameState.Available:
                AddAction(primarySection, actionGroup, "Download", "download", () => 
                {
                    libraryView.ShowGameDetailsView(gameModel);
                });
                break;
            case EGameState.Downloading:
                AddAction(primarySection, actionGroup, "Cancel Download", "cancel-download", () =>
                {
                    DownloadQueueService.CancelDownload(gameModel.Id, gameModel.Slug);
                });
                break;
            case EGameState.Downloaded:
                AddAction(primarySection, actionGroup, "Install", "install", () =>
                {
                    if (!LibraryService.Instance.TryGetGameEntry(gameModel.Id, out var gameEntry))
                        return;
                    
                    if (!DownloadQueueService.Instance.TryGetDownloadManifest(gameEntry.Slug, out var downloadManifest))
                        return;
                
                    _ = InstallerService.Instance.InstallAsync(gameEntry, downloadManifest);
                });
                break;
            case EGameState.Installed:
                AddAction(primarySection, actionGroup, "Play", "play", () =>
                {
                    if (!LibraryService.Instance.TryGetGameEntry(gameModel.Id, out var gameEntry))
                        return;
                    
                    _ = LauncherService.LaunchGameAsync(gameEntry, CancellationToken.None);
                });
                AddAction(primarySection, actionGroup, "Launch Parameters...", "launch-params", () => { });
                break;
            case EGameState.Running:
                AddAction(primarySection, actionGroup, "Stop Game", "stop-game", () =>
                {
                    // TODO: Implement stopping the game process...
                });
                break;
            case EGameState.Installing:
            case EGameState.Uninstalling:
                var busyItem = Gio.MenuItem.New($"{gameState}...", null);
                primarySection.AppendItem(busyItem);
                break;
        }

        if (primarySection.GetNItems() > 0)
        {
            rootMenu.AppendSection(null, primarySection);
        }
        
        var manageSection = Gio.Menu.New();

        if (gameState is EGameState.Installed or EGameState.Running)
        {
            AddAction(manageSection, actionGroup, "Open Install Folder", "open-folder", () =>
            {
                if (LibraryService.Instance.TryGetGameEntry(gameModel.Id, out var gameEntry) &&
                    InstallerService.Instance.TryGetInstallManifest(gameEntry.Slug, out var installManifest) &&
                    Directory.Exists(installManifest.InstallDirectoryPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{installManifest.InstallDirectoryPath}\"",
                        UseShellExecute = false
                    });
                }
            });
        }
        
        if (gameState is EGameState.Installed)
        {
            AddAction(manageSection, actionGroup, "Uninstall", "uninstall", () =>
            {
                if (!LibraryService.Instance.TryGetGameEntry(gameModel.Id, out var gameEntry))
                    return;
                
                ConfirmUninstallDialog.Show(libraryView.ParentWindow, gameEntry);
            });
        }
        
        AddAction(manageSection, actionGroup, "Game Details...", "details", () =>
        {
            libraryView.ShowGameDetailsView(gameModel);
        });

        rootMenu.AppendSection(null, manageSection);
        
        targetWidget.InsertActionGroup("game", actionGroup);
        return rootMenu;
    }

    private static void AddAction(Gio.Menu menu, Gio.SimpleActionGroup actionGroup, string label, string actionName, Action callback)
    {
        var action = Gio.SimpleAction.New(actionName, null);
        action.OnActivate += (_, _) => callback();
        actionGroup.AddAction(action);
        menu.Append(label, $"game.{actionName}");
    }
}