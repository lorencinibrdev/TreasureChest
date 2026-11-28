using Gtk;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Services.GOG;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Views.Library;

public static class LibraryItemHelper
{
    // Private Members:
    private static readonly Dictionary<IntPtr, (GameModel Model, Action Handler)> StateSubscriptions = [];

    public static void BindState(
        ListItem listItem,
        GameModel gameModel,
        Widget targetContainer,
        Picture picture,
        Widget stateIndicator,
        Adw.Spinner spinner,
        Action<string>? setIconName = null,
        Label? playtimeLabel = null
    )
    {
        UnbindState(listItem);

        Update();
        gameModel.Changed += Update;
        StateSubscriptions[listItem.Handle.DangerousGetHandle()] = (gameModel, Update);
        
        return;

        void Update()
        {
            var currentGameState = LibraryService.Instance.GetGameState(gameModel.Id);
            ApplyGameState(currentGameState, targetContainer, picture, stateIndicator, spinner, setIconName);

            if (playtimeLabel is null) 
                return;
            
            var playStas = LibraryService.Instance.GetGamePlayStats(gameModel.Id);
            var formattedTime = FormatShowPlayTime(playStas.TimePlayed);
            playtimeLabel.SetText(formattedTime);
            playtimeLabel.Visible = !string.IsNullOrEmpty(formattedTime);
        }
    }

    public static void UnbindState(ListItem listItem)
    {
        var handle = listItem.Handle.DangerousGetHandle();

        if (StateSubscriptions.Remove(handle, out var subscription))
        {
            subscription.Model.Changed -= subscription.Handler;
        }
    }
    
    public static void ApplyGameState(
        EGameState gameState,
        Widget targetContainer,
        Picture picture,
        Widget stateIndicator,
        Adw.Spinner spinner,
        Action<string>? setIconName = null
    )
    {
        switch (gameState)
        {
            case EGameState.Downloading:
            case EGameState.Installing:
                picture.Opacity = 0.5;
                stateIndicator.Visible = false;
                spinner.Visible = true;
                targetContainer.TooltipText = gameState == EGameState.Downloading
                    ? "Downloading Game..."
                    : "Installing Game...";
                break;
            case EGameState.Downloaded:
                picture.Opacity = 0.8;
                stateIndicator.Visible = true;
                setIconName?.Invoke("folder-download-symbolic");
                spinner.Visible = false;
                targetContainer.TooltipText = "Game is downloaded, and ready to Install.";
                break;
            case EGameState.Installed:
                picture.Opacity = 1.0;
                stateIndicator.Visible = false;
                spinner.Visible = false;
                targetContainer.TooltipText = "Game is installed, and ready to Play";
                break;
            case EGameState.Running:
                break;
            case EGameState.Uninstalling:
                break;
            case EGameState.Available:
            default:
                picture.Opacity = 0.3;
                stateIndicator.Visible = true;
                setIconName?.Invoke("cloud-symbolic");
                spinner.Visible = false;
                targetContainer.TooltipText = "Game is available for download.";
                break;
        }
    }

    public static async Task BindImageAsync(
        ListItem listItem,
        GameModel game,
        Picture picture,
        EImageType imageType,
        string? fallbackUrl
    )
    {
        if (ImagePrefetchService.Instance.TryGetCachedImagePath(game.Id, imageType, out var localPath))
        {
            picture.SetFilename(localPath);
            return;
        }

        picture.SetFile(null);
        
        var localImagePath = await ImagePrefetchService.Instance.FetchImageWithPriorityAsync(game.Id, imageType, fallbackUrl);

        if (listItem.Item == game && File.Exists(localImagePath))
        {
            picture.SetFilename(localImagePath);
        }
    }

    public static string FormatShowPlayTime(TimeSpan timePlayed)
    {
        if (timePlayed.TotalMinutes < 1)
            return string.Empty;

        if (timePlayed.TotalHours < 1)
            return $"{(int)timePlayed.TotalMinutes}m";
        
        return $"{timePlayed.TotalHours:F1}h";
    }
}