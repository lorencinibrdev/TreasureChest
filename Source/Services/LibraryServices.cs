using System.Text.Json;
using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Services;

public static class LibraryServices
{
    // Private Members:
    private static readonly Dictionary<long, GameDetailsResponse> GameEntries = [];
    private static readonly SettingsService Settings = new();
    
    public static void AddGames(List<GameDetailsResponse> games)
    {
        foreach (var game in games)
        {
            if (game.Id is null)
                continue;
            
            GameEntries[game.Id.Value] = game;
        }
    }

    public static IReadOnlyList<GameDetailsResponse> GetLoadedGames()
    {
        return [.. GameEntries.Values];
    }

    public static GameDetailsResponse? GetGameDetails(long gameId)
    {
        return GameEntries.GetValueOrDefault(gameId);
    }

    public static async Task SaveLibraryAsync()
    {
        await Console.Out.WriteAsync($"[INFO] - Saving the user library to the file: '{LibraryFilePath}'...");
        
        await using var createStream = File.Create(LibraryFilePath);
        await JsonSerializer.SerializeAsync(createStream, GameEntries, new JsonSerializerOptions { WriteIndented = true });
        
        await Console.Out.WriteLineAsync("\tDone.");
    }

    public static async Task<List<GameDetailsResponse>?> LoadLibraryFromCacheAsync()
    {
        await Console.Out.WriteLineAsync($"[INFO] - Loading the user library from the cached file: '{LibraryFilePath}'...");
        
        if (!File.Exists(LibraryFilePath))
            return null;
        
        try
        {
            await using var fileStream = File.OpenRead(LibraryFilePath);
            var loadedLibrary =  await JsonSerializer.DeserializeAsync<Dictionary<long, GameDetailsResponse>>(fileStream);

            if (loadedLibrary is null)
                return null;

            foreach (var (id, game) in loadedLibrary)
            {
                GameEntries[id] = game;
            }
            
            return [.. GameEntries.Values];
            
        }
        catch(Exception e)
        {
            await Console.Out.WriteLineAsync($"[WARN] - Could not parse the cached library file: '{LibraryFilePath}'.");
            await Console.Out.WriteLineAsync(e.Message);
            return null;
        }
    }
    
    private static string SetupLibraryFilePath()
    {
        var libraryFileName = $"user_library_{Settings.SavedUserId}.json";
        var libraryFilePath = Path.Combine(SettingsService.DataDirectory, libraryFileName);
        return libraryFilePath;
    }
    
    // Properties:
    public static int GamesCount => GameEntries.Count;
    private static string LibraryFilePath { get; } = SetupLibraryFilePath();
}