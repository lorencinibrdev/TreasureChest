using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TreasureChest.Source.Helpers;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Downloads;
using TreasureChest.Source.Services.Install;
using TreasureChest.Source.Services.Library.Records;

namespace TreasureChest.Source.Services.Library;

public class LibraryService
{
    // Private Members:
    private readonly Dictionary<long, GameEntry> _gameEntries = [];
    private readonly Dictionary<long, GamePlayStats> _gamePlayStats = [];
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

        _ = LoadPlayStatsAsync();
    }

    public void AddGames(List<GogResponseGameDetails> gameResponseList)
    {
        foreach (var gameResponse in gameResponseList)
        {
            // Merging the raw information from the API:
            if (gameResponse.Id is null)
                continue;
            
            _gameDetailsRawResponses[gameResponse.Id.Value] = gameResponse;

            var playStats = GetGamePlayStats(gameResponse.Id.Value);
            
            // Loading the actual game object:
            var gameEntry = GameEntry.TryCreate(gameResponse, playStats);
            
            if (gameEntry is null)
                continue;
            
            _gameEntries[gameEntry.Id] = gameEntry;
            _gamePlayStats[gameEntry.Id] = gameEntry.PlayStats;
        }
    }

    public async Task CommitGamesAsync(List<GogResponseGameDetails> gameResponseList, bool saveToDisk)
    {
        AddGames(gameResponseList);
        await InitializeGameStatesFromDiskAsync();

        if (saveToDisk)
        {
            await SaveLibraryAsync();
        }
        
        LibraryUpdated?.Invoke();
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
            await LoadPlayStatsAsync();
            
            await using var fileStream = File.OpenRead(LibraryFilePath);
            var loadedLibrary = await JsonSerializer.DeserializeAsync<List<GogResponseGameDetails>>(fileStream);

            if (loadedLibrary is null)
                return false;

            AddGames(loadedLibrary);

            await InitializeGameStatesFromDiskAsync();

            await Console.Out.WriteLineAsync($"[INFO] - Successfully loaded the cached library file. {loadedLibrary.Count} games loaded from disk.");
            
            LibraryUpdated?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARN] - Could not parse the cached library file: '{LibraryFilePath}'.");
            await Console.Out.WriteLineAsync(e.Message);
            return false;
        }
    }

    public async Task LoadPlayStatsAsync()
    {
        if (!File.Exists(PlayStatsFilePath))
            return;

        await _fileLock.WaitAsync();

        try
        {
            await using var fileStream = File.OpenRead(PlayStatsFilePath);
            var loadedStats = await JsonSerializer.DeserializeAsync<Dictionary<long, GamePlayStats>>(fileStream);

            if (loadedStats is null)
                return;

            _gamePlayStats.Clear();

            foreach (var (gameId, gamePlayStats) in loadedStats)
            {
                _gamePlayStats[gameId] = gamePlayStats;
            }
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARN] - Could not parse the play stats file: '{PlayStatsFilePath}'.");
            await Console.Out.WriteLineAsync(e.Message);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SavePlayStatsAsync()
    {
        await _fileLock.WaitAsync();

        try
        {
            var tempPath = $"{PlayStatsFilePath}.tmp";

            await using (var createStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(createStream, _gamePlayStats, _serializerOptions);
            }

            File.Move(tempPath, PlayStatsFilePath, true);
        }
        finally
        {
            _fileLock.Release();
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
    
    public GamePlayStats GetGamePlayStats(long gameId)
    {
        return _gamePlayStats.GetValueOrDefault(gameId, new GamePlayStats(TimeSpan.Zero, null));
    }

    public async Task RecordPlaySessionAsync(long gameId, TimeSpan sessionDuration, DateTimeOffset lastPlayed)
    {
        // Ignoring small play times (maybe game crashed)
        if (sessionDuration.TotalSeconds < 10)
            return;

        if (!TryGetGameEntry(gameId, out var gameEntry))
            return;

        var current = GetGamePlayStats(gameId);
        var updated = new GamePlayStats(
            current.TimePlayed + sessionDuration,
            lastPlayed
        );
        
        _gamePlayStats[gameId] = updated;
        gameEntry.PlayStats = updated;
        
        GamePlayStatsChanged?.Invoke(gameId, updated);
        await SavePlayStatsAsync();
    }

    private void RefreshGameState(long gameId)
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
        if (InstallerService.Instance.TryGetInstallManifest(game.Slug, out _))
            return EGameState.Installed;
        
        // Downloading?
        if (DownloadQueueService.Instance.IsDownloadingGame(game.Id))
            return EGameState.Downloading;

        return DownloadQueueService.Instance.IsGameDownloaded(game.Slug) 
            ? EGameState.Downloaded 
            : EGameState.Available;
    }
    
    // Properties:
    public static LibraryService Instance { get; } = new();
    public int GamesCount => _gameEntries.Count;
    private static string LibraryFilePath => Path.Combine(SettingsService.DataDirectory, $"user_library_{SettingsService.Instance.SavedUserId}.json");
    private static string PlayStatsFilePath => Path.Combine(SettingsService.DataDirectory, $"user_playstats_{SettingsService.Instance.SavedUserId}.json");
    
    // Events for the library handling:
    public event Action? LibraryUpdated;
    public event Action<long, EGameState>? GameStateChanged;
    public event Action<long, GamePlayStats>? GamePlayStatsChanged;
}