using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Services;

public static class GogServices
{
    // Consts:
    private const string ClientId = "46899977096215655";
    private const string ClientSecret = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9";
    private const string RedirectUri = "https://embed.gog.com/on_login_success?origin=client";
    
    // Private Members:
    private static readonly HttpClient HttpClient = new();
    private static GogTokenResponse? _tokens;
    private static GogUserDataResponse? _userData;

    public static async Task<bool> Login(string? authCode)
    {
        Secret.Module.Initialize();

        // If we did not get an authCode, then login modal either failed or was canceled.
        if (string.IsNullOrEmpty(authCode))
        {
            await Console.Out.WriteLineAsync("[INFO] - Login failed or cancelled from modal dialog.");
            return false;
        }
        
        // With the authCode, we can request the refresh tokens from GOG API's:
        await RequestTokens(authCode);

        if (_tokens is null || !_tokens.IsValid())
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to retrieve the Login Tokens from GOG API's. Cannot Login.");
            return false;
        }
        
        // If we reach here, then the tokens were successfully retrieved, and we can fetch the user details:
        await RefreshUserInformation();
        
        // Finally, if we reach here, then login was successful, and all information was fetched. Saving it on the user's secret service:
        await Console.Out.WriteLineAsync("[INFO] - Login successful!");
        await Console.Out.WriteLineAsync($"[INFO] - Username: {_userData?.Username}, Id: {_userData?.UserId}, Email: {_userData?.Email}");
        await Console.Out.WriteLineAsync("[INFO]: Saving the refresh token to the system's secret service...");
        
        await SaveRefreshTokenToKeyring();
        
        return true;
    }
    
    public static async Task<bool> RestoreSessionAsync()
    {
        // If we are not logged in, then cannot restore session:
        if (!ConfigurationService.Current.IsLoggedIn || string.IsNullOrEmpty(ConfigurationService.Current.SavedUserName))
            return false;

        Secret.Module.Initialize();
        
        if (_userData is null || !_userData.IsValid())
        {
            _userData = new GogUserDataResponse
            {
                Username = ConfigurationService.Current.SavedUserName,
                UserId = ConfigurationService.Current.SavedUserId,
                Email = ConfigurationService.Current.SavedUserEmail
            };
        }
        
        await Console.Out.WriteLineAsync($"[INFO] - Silently restoring user session for User: {_userData.Username}, Id: {_userData.UserId}...");

        if (!await RefreshTokens())
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the session. User will need to login again...");
            
            ConfigurationService.Current.IsLoggedIn = false;
            ConfigurationService.Current.SavedUserName = string.Empty;
            ConfigurationService.Current.SavedUserId = string.Empty;
            ConfigurationService.Current.SavedUserEmail = string.Empty;
            ConfigurationService.SaveConfigurationToJsonFile();
            return false;
        }

        await RefreshUserInformation();
        
        await Console.Out.WriteLineAsync("[INFO] - Silent Login successful!");
        return true;
    }

    private static async Task<bool> RequestTokens(string authCode)
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetching the user tokens (First Login)...");
        
        var requestBody = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "client_secret", ClientSecret },
            { "grant_type", "authorization_code" },
            { "code", authCode },
            { "redirect_uri", RedirectUri },
        };
        
        _tokens = (GogTokenResponse?)await CallPostApi<GogTokenResponse>(UrlTokenFetch, requestBody);

        if (_tokens is not null && _tokens.IsValid()) 
            return true;
        
        await Console.Out.WriteLineAsync("[ERROR] - Failed to request new tokens...");
        return false;

    }

    private static async Task<bool> RefreshTokens()
    {
        // Trying to fetch from secret keyring:
        await Console.Out.WriteLineAsync("[INFO] - Fetching the refresh tokens from the secret keyring...");

        var userName = _userData?.Username ?? ConfigurationService.Current.SavedUserName;
        var oldRefreshToken = _tokens?.RefreshToken ?? await SecretService.GetRefreshTokenAsync(userName); 
        
        if (string.IsNullOrEmpty(oldRefreshToken))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the refresh token. This should not happen,and a new Login will be necessary.");
            ConfigurationService.Current.IsLoggedIn = false;
            ConfigurationService.Current.SavedUserName = string.Empty;
            ConfigurationService.Current.SavedUserId = string.Empty;
            ConfigurationService.Current.SavedUserEmail = string.Empty;
            return false;
        }

        var requestBody = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "client_secret", ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", oldRefreshToken }
        };

        await Console.Out.WriteLineAsync("[INFO] - Fetching the user tokens (Refresh)...");

        _tokens = (GogTokenResponse?)await CallPostApi<GogTokenResponse>(UrlTokenFetch, requestBody); 

        if (_tokens is null || !_tokens.IsValid()) 
            return false;
        
        await SaveRefreshTokenToKeyring();
        
        return true;
    }

    public static async Task<bool> RefreshGameList()
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetching the game list from GOG servers...");

        var gameResponse = (OwnedGamesResponse?) await CallGetApi<OwnedGamesResponse>(UrlGameList);

        if (gameResponse?.Games is null)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to fetch the game list.");
            return false;
        }
        
        await Console.Out.WriteLineAsync($"[INFO] - Successfully retrieved the game list. Found {gameResponse.Games.Count} games.");
        await ProcessGameList(gameResponse);
        
        // Saving the gamelist information on the local cache:
        await LibraryServices.SaveLibraryAsync();
        
        return true;
    }

    private static async Task<bool> ProcessGameList(OwnedGamesResponse gameList)
    {
        if (gameList.Games is null)
            return false;
        
        var batches = gameList.Games.Chunk(50).ToList();
        var count = 1;

        foreach (var batch in batches)
        {
            await Console.Out.WriteAsync($"[INFO] - Fetching game details (Batch {count} of {batches.Count})...)");
            
            var idList = string.Join(",", batch);
            var url = $"https://embed.gog.com/products?ids={idList}";
            
            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Console.Out.WriteLineAsync($"[ERROR] - GOG API returned {response.StatusCode}: {error}");
            }
            
            response.EnsureSuccessStatusCode();

            var fetchedDetails = await response.Content.ReadFromJsonAsync<List<GameDetailsResponse>>();

            if (fetchedDetails is not null)
            {
                LibraryServices.AddGames(fetchedDetails);
            }
            
            await Console.Out.WriteLineAsync("\tDone.");
            count++;
        }
        
        return true;
    }

    private static async Task<bool> RefreshUserInformation()
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetching the user information...");
        
        _userData = (GogUserDataResponse?)await CallGetApi<GogUserDataResponse>(UrlUserData);

        if (_userData is null || !_userData.IsValid())
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to fetch the user information.");
            return false;
        }
        
        // Saving into the configuration manager:
        ConfigurationService.Current.SavedUserName = _userData.Username!;
        ConfigurationService.Current.SavedUserId = _userData.UserId!;
        ConfigurationService.Current.SavedUserEmail = _userData.Email!;
        ConfigurationService.Current.IsLoggedIn = true;
        ConfigurationService.SaveConfigurationToJsonFile();
        return true;
    }

    private static async Task<bool> SaveRefreshTokenToKeyring()
    {
        await Console.Out.WriteLineAsync("[INFO] - Saving the user information on the system keyring...");

        var username = _userData?.Username;
        var refreshToken = _tokens?.RefreshToken;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(refreshToken))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Requested to save data to Secret Keyring, but data is empty/null.");
            return false;
        }

        var saved = false;
        
        try
        {
            saved = await SecretService.SaveRefreshTokenAsync(username, refreshToken);

            if (saved)
            {
                await Console.Out.WriteLineAsync("[INFO] - Successfully stored the refresh token on the system LibSecret.");
            }
        }
        catch
        {
            // ignored
        }
        
        return saved;
    }

    private static async Task<object?> CallGetApi<T>(string url)
    {
        if (_tokens is null || string.IsNullOrEmpty(_tokens.AccessToken))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Stored access token is empty or null. Cannot make API call");
            return null;
        }
        
        await Console.Out.WriteLineAsync($"[INFO] - Sending GET request to {url}...");
        
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
            
            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var jsonText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(jsonText);
        }
        catch
        {
            // ignored
        }

        return null;
    }
    
    private static async Task<object?> CallPostApi<T>(string url, Dictionary<string, string> requestBody)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Sending POST request to {url}...");

        try
        {
            var response = await HttpClient.PostAsync(url, new FormUrlEncodedContent(requestBody));

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Console.Out.WriteLineAsync($"[ERROR] - API returned {response.StatusCode}: {error}");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var jsonText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(jsonText);
        }
        catch
        {
            // ignored
        }
        
        return null;
    }
    
    // Properties:
    public static string UrlAuth { get; } = $"https://auth.gog.com/auth?client_id={ClientId}&redirect_uri={RedirectUri}&response_type=code&layout=client2";
    public static string UrlTokenFetch { get; } = "https://auth.gog.com/token";
    public static string UrlUserData { get; } = "https://embed.gog.com/userData.json";
    public static string UrlGameList { get; } = "https://embed.gog.com/user/data/games";
}