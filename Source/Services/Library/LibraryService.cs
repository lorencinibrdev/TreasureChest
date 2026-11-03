using System.Text.Json;
using TreasureChest.Source.Helpers.GOG.Responses;

namespace TreasureChest.Source.Services.Library;

public class LibraryService
{
    // Private Members:
    private readonly Dictionary<long, GameEntry> _gameEntries = [];
    private readonly Dictionary<long, GogResponseGameDetails> _gameDetailsRawResponses = [];
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private LibraryService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
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

    public GameEntry? GetGameDetails(long gameId)
    {
        return _gameEntries.GetValueOrDefault(gameId);
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
    
    // Properties:
    public static LibraryService Instance { get; } = new();
    public int GamesCount => _gameEntries.Count;
    private static string LibraryFilePath => Path.Combine(SettingsService.DataDirectory, $"user_library_{SettingsService.Instance.SavedUserId}.json");
}