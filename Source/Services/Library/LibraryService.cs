using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.Install;

namespace TreasureChest.Source.Services.Library;

public class LibraryService
{
    // Private Members:
    private readonly Dictionary<long, GameEntry> _gameEntries = [];
    private readonly ConcurrentDictionary<long, EGameState> _gameStates = [];
    private readonly Dictionary<long, GogResponseGameDetails> _gameDetailsRawResponses = [];
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private LibraryService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        
        DownloadQueueService.Instance.TaskAdded += HandleDownloadStateChanged;
        DownloadQueueService.Instance.TaskCompleted += HandleDownloadStateChanged;
    }

    public void AddGames(List<GogResponseGameDetails> gameResponseList)
    {
        foreach (var gameResponse in gameResponseList)
        {
            // Merging the raw information from the API:
            if (gameResponse.Id is null)
                continue;
            
            _gameDetailsRawResponses[gameResponse.Id.Value] = gameResponse;
            
            // Loading the actual game object:
            var gameEntry = GameEntry.TryCreate(gameResponse);
            
            if (gameEntry is null)
                continue;
            
            _gameEntries[gameEntry.Id] = gameEntry;
        }
    }

    public IReadOnlyList<GameEntry> GetLoadedGames()
    {
        return [.. _gameEntries.Values];
    }

    public bool TryGetGameEntry(long gameId, [NotNullWhen(true)] out GameEntry? gameEntry)
    {
        if (_gameEntries.TryGetValue(gameId, out var foundGame))
        {
            gameEntry = foundGame;
            return true;
        }

        gameEntry = null;
        return false;
    }

    public async Task SaveLibraryAsync()
    {
        await Console.Out.WriteLineAsync($"[INFO] - Saving the user library to the file: '{LibraryFilePath}'...");

        await _fileLock.WaitAsync();

        try
        {
            await using var createStream = File.Create(LibraryFilePath);
            await JsonSerializer.SerializeAsync(createStream, _gameDetailsRawResponses.Values, _serializerOptions);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> LoadLibraryFromCacheAsync()
    {
        await Console.Out.WriteLineAsync($"[INFO] - Loading the user library from the cached file: '{LibraryFilePath}'...");
        
        if (!File.Exists(LibraryFilePath))
        {
            await Console.Out.WriteLineAsync("[WARN] - Cached library file does not exists...");
            return false;
        }
        
        try
        {
            await using var fileStream = File.OpenRead(LibraryFilePath);
            var loadedLibrary =  await JsonSerializer.DeserializeAsync<List<GogResponseGameDetails>>(fileStream);

            if (loadedLibrary is null)
                return false;

            AddGames(loadedLibrary);
            
            await Console.Out.WriteLineAsync($"[INFO] - Successfully loaded the cached library file. {loadedLibrary.Count} games loaded from disk.");
            return true;
        }
        catch(Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARN] - Could not parse the cached library file: '{LibraryFilePath}'.");
            await Console.Out.WriteLineAsync(e.Message);
            return false;
        }
    }

    public async Task InitializeGameStatesFromDiskAsync()
    {
        await Console.Out.WriteLineAsync("[INFO] - Initializing the game states from the disk...");

        await Task.Run(() =>
            {
                foreach (var game in _gameEntries.Values)
                {
                    var state = EvaluateGameStateFromDisk(game);
                    _gameStates[game.Id] = state;
                }   
            }
        );
        
        await Console.Out.WriteLineAsync("[INFO] - Game states initialized.");
    }
    
    public void SetGameState(long gameId, EGameState gameState)
    {
        if (!_gameStates.ContainsKey(gameId))
            return;
        
        _gameStates[gameId] = gameState;
        GameStateChanged?.Invoke(gameId, gameState);
    }

    public EGameState GetGameState(long gameId)
    {
        return _gameStates.GetValueOrDefault(gameId, EGameState.Available);
    }

    public void RefreshGameState(long gameId)
    {
        if (!_gameEntries.TryGetValue(gameId, out var gameEntry))
            return;

        var newState = EvaluateGameStateFromDisk(gameEntry);

        if (_gameStates.TryGetValue(gameId, out var oldState) && oldState == newState)
            return;
        
        _gameStates[gameId] = newState;
        GameStateChanged?.Invoke(gameId, newState);
    }
    
    private void HandleDownloadStateChanged(DownloadTask downloadTask)
    {
        RefreshGameState(downloadTask.GameId);
    }

    private static EGameState EvaluateGameStateFromDisk(GameEntry game)
    {
        // Installed?
        if (InstallerService.Instance.IsGameInstalled(game.Slug))
            return EGameState.Installed;
        
        // Downloading?
        if (DownloadQueueService.Instance.IsDownloadingGame(game.Id))
            return EGameState.Downloading;
        
        var sourceDir = Path.Join(SettingsService.Instance.DownloadsFolder, game.Slug);
        var (type, _) = InstallerService.IdentifyGameInstallationType(game, sourceDir);
    
        if (type != EGameInstallationType.Unknown)
            return EGameState.Downloaded;

        // 4. Fallback: Just sitting in the GOG library
        return EGameState.Available;
    }
    
    // Properties:
    public static LibraryService Instance { get; } = new();
    public int GamesCount => _gameEntries.Count;
    private static string LibraryFilePath => Path.Combine(SettingsService.DataDirectory, $"user_library_{SettingsService.Instance.SavedUserId}.json");
    
    // Events for the library handling:
    public event Action<long, EGameState>? GameStateChanged;
}