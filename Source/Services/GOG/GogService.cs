using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TreasureChest.Source.Helpers.GOG.Responses;
using TreasureChest.Source.Services.Library;

namespace TreasureChest.Source.Services.GOG;

public class GogService
{
    // Consts:
    private const string ClientId = "46899977096215655";
    private const string ClientSecret = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9";
    private const string RedirectUri = "https://embed.gog.com/on_login_success?origin=client";
    
    // Private Members:
    private readonly HttpClient _httpClient;
    private GogTokenResponse? _tokens;
    private GogUserDataResponse? _userData;

    private GogService()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
    }
    
    public async Task<bool> Login(string? authCode)
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

        if (_tokens is null || !_tokens.IsValid)
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
    
    public async Task<bool> RestoreSessionAsync()
    {
        // If we are not logged in, then cannot restore session:
        if (string.IsNullOrEmpty(SettingsService.Instance.SavedUsername))
            return false;

        Secret.Module.Initialize();
        
        if (_userData is null || !_userData.IsValid)
        {
            _userData = new GogUserDataResponse
            {
                Username = SettingsService.Instance.SavedUsername,
                UserId = SettingsService.Instance.SavedUserId,
                Email = SettingsService.Instance.SavedUserEmail
            };
        }
        
        await Console.Out.WriteLineAsync($"[INFO] - Silently restoring user session for User: {_userData.Username}, Id: {_userData.UserId}...");

        if (!await RefreshTokens())
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the session. User will need to login again...");
            
            IsLoggedIn = false;
            SettingsService.Instance.SavedUsername = string.Empty;
            SettingsService.Instance.SavedUserId = string.Empty;
            SettingsService.Instance.SavedUserEmail = string.Empty;
            
            return false;
        }

        await RefreshUserInformation();
        
        await Console.Out.WriteLineAsync("[INFO] - Silent Login successful!");
        return true;
    }

    private async Task<bool> RequestTokens(string authCode)
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

        if (_tokens is not null && _tokens.IsValid) 
            return true;
        
        await Console.Out.WriteLineAsync("[ERROR] - Failed to request new tokens...");
        return false;

    }

    private async Task<bool> RefreshTokens()
    {
        // Trying to fetch from secret keyring:
        await Console.Out.WriteLineAsync("[INFO] - Fetching the refresh tokens from the secret keyring...");

        var userName = _userData?.Username ?? SettingsService.Instance.SavedUsername;
        var oldRefreshToken = _tokens?.RefreshToken ?? await SecretService.Instance.GetRefreshTokenAsync(userName); 
        
        if (string.IsNullOrEmpty(oldRefreshToken))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the refresh token. This should not happen,and a new Login will be necessary.");
            IsLoggedIn = false;
            SettingsService.Instance.SavedUsername = string.Empty;
            SettingsService.Instance.SavedUserId = string.Empty;
            SettingsService.Instance.SavedUserEmail = string.Empty;
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

        if (_tokens is null || !_tokens.IsValid) 
            return false;
        
        await SaveRefreshTokenToKeyring();
        
        return true;
    }

    public async Task<bool> RefreshGameList()
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetching the game list from GOG servers...");

        var gameResponse = (GogOwnedGamesListResponse?) await CallGetApi<GogOwnedGamesListResponse>(UrlGameList);

        if (gameResponse?.Games is null)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Failed to fetch the game list.");
            return false;
        }
        
        await ProcessGameList(gameResponse);
        await Console.Out.WriteLineAsync($"[INFO] - Successfully retrieved the game list. Found {LibraryService.Instance.GamesCount} games.");
        
        // Saving the gamelist information on the local cache:
        await LibraryService.Instance.SaveLibraryAsync();
        
        return true;
    }

    public async Task<GogResponseGameContent?> FetchGameContentDetails(GameEntry game)
    {
        var urlToCall = $"{UrlGameDetails}{game.Id.ToString()}.json";
        return await CallGetApi<GogResponseGameContent>(urlToCall) as GogResponseGameContent;
    }
    
    public async Task<List<GogResponseGameDetails>?> FetchGameDlcContentDetails(GameEntry game)
    {
        if (!game.HasDlc || string.IsNullOrWhiteSpace(game.GameLinks.AllProductsUrl))
            return null;

        return await CallGetApi<List<GogResponseGameDetails>>(game.GameLinks.AllProductsUrl) as List<GogResponseGameDetails>;
    }

    public async Task<GogProductDescriptionResponse?> FetchGameDescriptionAsync(long gameId)
    {
        var url = $"https://api.gog.com/products/{gameId}?expand=description";
        return await CallGetApi<GogProductDescriptionResponse>(url) as GogProductDescriptionResponse;
    }
    
    public async Task<string> ResolveFinalDownloadUrlAsync(string manualUrl)
    {
        var requestUrl = manualUrl.StartsWith('/')
            ? $"https://www.gog.com{manualUrl}"
            : manualUrl;

        var response = await CallGetApi(requestUrl);

        if (response is null || (int)response.StatusCode is < 300 or > 399)
            return requestUrl;

        var redirectUri = response.Headers.Location;

        if (redirectUri is not null)
        {
            return redirectUri.IsAbsoluteUri
                ? redirectUri.ToString()
                : $"https://www.gog.com{{redirectUri}}";
        }

        return requestUrl;
    }

    private async Task<bool> ProcessGameList(GogOwnedGamesListResponse gameList)
    {
        if (gameList.Games is null)
            return false;
        
        var batches = gameList.Games.Chunk(50).ToList();
        var count = 1;

        foreach (var batch in batches)
        {
            await Console.Out.WriteAsync($"[INFO] - Fetching game details (Batch {count} of {batches.Count})...)");
            
            var idList = string.Join(",", batch);
            var url = $"https://embed.gog.com/products?ids={idList}?expand=description";
            
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Console.Out.WriteLineAsync($"[ERROR] - GOG API returned {response.StatusCode}: {error}");
            }
            
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var fetchedDetails = await response.Content.ReadFromJsonAsync<List<GogResponseGameDetails>>();

            if (fetchedDetails is not null)
            {
                var onlyBaseGames = fetchedDetails
                    .Where(p => p.GameType is "game" or "pack").ToList();

                if (onlyBaseGames.Count > 0)
                {
                    LibraryService.Instance.AddGames(onlyBaseGames);    
                }
            }
            
            await Console.Out.WriteLineAsync("\tDone.");
            count++;
        }
        
        return true;
    }

    private async Task<bool> RefreshUserInformation()
    {
        await Console.Out.WriteLineAsync("[INFO] - Fetching the user information...");
        
        _userData = (GogUserDataResponse?)await CallGetApi<GogUserDataResponse>(UrlUserData);

        if (_userData is null || !_userData.IsValid)
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to fetch the user information.");
            return false;
        }
        
        // Saving into the configuration manager:
        SettingsService.Instance.SavedUsername = _userData.Username!;
        SettingsService.Instance.SavedUserId = _userData.UserId!;
        SettingsService.Instance.SavedUserEmail = _userData.Email!;
        IsLoggedIn = true;
        
        return true;
    }

    private async Task<bool> SaveRefreshTokenToKeyring()
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
            saved = await SecretService.Instance.SaveRefreshTokenAsync(username, refreshToken);

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

    private async Task<object?> CallGetApi<T>(string url)
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
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var jsonText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(jsonText);
        }
        catch(Exception e)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Got exception: {e.Message}");
        }

        return null;
    }
    
    private async Task<HttpResponseMessage?> CallGetApi(string url)
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
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return response;
        }
        catch(Exception e)
        {
            await Console.Out.WriteLineAsync($"[ERROR] - Got exception: {e.Message}");
        }

        return null;
    }
    
    private async Task<object?> CallPostApi<T>(string url, Dictionary<string, string> requestBody)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Sending POST request to {url}...");

        try
        {
            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(requestBody));

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
    public static GogService Instance { get; } = new();
    public bool IsLoggedIn { get; private set; }
    public string UrlAuth { get; } = $"https://auth.gog.com/auth?client_id={ClientId}&redirect_uri={RedirectUri}&response_type=code&layout=client2";
    private string UrlTokenFetch { get; } = "https://auth.gog.com/token";
    private string UrlUserData { get; } = "https://embed.gog.com/userData.json";
    private string UrlGameList { get; } = "https://embed.gog.com/user/data/games";
    private string UrlGameDetails { get; } = "https://embed.gog.com/account/gameDetails/";
}