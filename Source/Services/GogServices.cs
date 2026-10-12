using System.Net.Http.Headers;
using System.Net.Http.Json;
using TreasureChest.Source.Helpers.GOG;

namespace TreasureChest.Source.Services;

public static class GogServices
{
    // Private Members:
    private static readonly HttpClient HttpClient = new();
    private const string GogClientId = "46899977096215655";
    private const string GogClientSecret = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9";
    private const string RedirectUri = "https://embed.gog.com/on_login_success?origin=client";
    private static GogTokenResponse _tokens = new();
    private static GogUserData _userData = new();

    public static async Task<bool> RestoreSessionAsync()
    {
        if (!ConfigurationService.Current.IsLoggedIn || string.IsNullOrEmpty(ConfigurationService.Current.SavedUserName))
            return false;
        
        await Console.Out.WriteLineAsync("[INFO] - Silently restoring user session...");
        
        Secret.Module.Initialize();

        var tokensRefreshed = await RefreshTokens();

        if (!tokensRefreshed)
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the session. User will need to login again...");
            
            ConfigurationService.Current.IsLoggedIn = false;
            ConfigurationService.SaveConfigurationToJsonFile();
            return false;
        }

        await RefreshUserData();
        
        await Console.Out.WriteLineAsync("[INFO] - Silent Login successful!");
        return true;
    }
    
    public static async Task<bool> ProcessLoginRequest(string authCode)
    {
        Secret.Module.Initialize();

        // If we did not get an authCode, then login modal either failed or was canceled.
        if (string.IsNullOrEmpty(authCode))
        {
            await Console.Out.WriteLineAsync("[INFO] - Login failed or cancelled.");
            return false;
        }
        
        // With the authCode, we can request the refresh tokens from GOG API's:
        var tokensFetched = await RequestTokens(authCode);

        if (!tokensFetched ||
            _tokens.AccessToken is null || string.IsNullOrEmpty(_tokens.AccessToken) ||
            _tokens.RefreshToken is null || string.IsNullOrEmpty(_tokens.RefreshToken))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to retrieve the Login Tokens from GOG API's. Cannot Login.");
            return false;
        }
        
        // If we reach here, then the tokens were sucessfully retrieved, and we can fetch the user details:
        var userInfoFetched = await RequestUserInformation();

        if (!userInfoFetched ||
            _userData.UserId is null || string.IsNullOrEmpty(_userData.UserId) ||
            _userData.Username is null || string.IsNullOrEmpty(_userData.Username))
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to retrieve the User Information from GOG API's. Cannot Login.");
            return false;
        }
        
        // Saving:
        ConfigurationService.Current.SavedUserName = _userData.Username;
        ConfigurationService.Current.SavedUserId = _userData.UserId;
        ConfigurationService.Current.IsLoggedIn = true;
        ConfigurationService.SaveConfigurationToJsonFile();
        
        // Finally, if we reach here, then login was sucessfull, and all information was fetched. Saving it on the user's secret service:
        await Console.Out.WriteLineAsync("[INFO] - Login successful!");
        await Console.Out.WriteLineAsync($"[INFO] - Username: {_userData.Username}, Id: {_userData.UserId}");

        await Console.Out.WriteLineAsync("[INFO]: Saving the refresh token to the system's secret service...");
        var informationSaved = await SaveRefreshTokenToKeyring();

        if (!informationSaved)
        {
            await Console.Out.WriteLineAsync("[INFO] - Failed to store the refresh token on the system's secret.");
            return false;
        }

        await Console.Out.WriteLineAsync("[INFO] - Successfully stored the refresh token on the system LibSecret.");
        return true;
    }

    public static async Task<bool> RefreshUserData()
    {
        var userInfoFetched = await RequestUserInformation();

        await  Console.Out.WriteLineAsync("[INFO] - Refreshing the user data...");

        if (userInfoFetched &&
            _userData.UserId is not null && !string.IsNullOrEmpty(_userData.UserId) &&
            _userData.Username is not null && !string.IsNullOrEmpty(_userData.Username)) 
            return true;
        
        await Console.Out.WriteLineAsync("[ERROR] - Failed to retrieve the User Information from GOG API's. Cannot Login.");
        return false;
    }

    private static async Task<bool> RequestTokens(string authCode)
    {
        var requestBody = new Dictionary<string, string>
        {
            { "client_id", GogClientId },
            { "client_secret", GogClientSecret },
            { "grant_type", "authorization_code" },
            { "code", authCode },
            { "redirect_uri", RedirectUri },
        };
        
        await Console.Out.WriteLineAsync("[INFO] - Fetching the user tokens...");
        return await InternalFetchUserTokens(requestBody);
    }
    
    public static async Task<bool> RefreshTokens()
    {
        //Fetching from the secret:
        var oldRefreshToken = string.IsNullOrEmpty(_tokens.RefreshToken)
            ? await SecretService.GetRefreshTokenAsync(_userData.Username ?? ConfigurationService.Current.SavedUserName)
            : _tokens.RefreshToken;

        if (oldRefreshToken is null)
        {
            await Console.Out.WriteLineAsync("[ERROR] - Failed to refresh the refresh token. This should not happen,a nd a new Login will be necessary.");
            return false;
        }

        var requestBody = new Dictionary<string, string>
        {
            { "client_id", GogClientId },
            { "client_secret", GogClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", oldRefreshToken }
        };

        await Console.Out.WriteLineAsync("[INFO] - Fetching the user tokens (Refresh)...");

        if (!await InternalFetchUserTokens(requestBody)) 
            return false;
        
        await SaveRefreshTokenToKeyring();
        return true;
    }

    private static async Task<bool> InternalFetchUserTokens(Dictionary<string, string> requestBody)
    {
        const string url = "https://auth.gog.com/token";
        
        await Console.Out.WriteLineAsync($"[INFO] - Sending request to {url}...");

        try
        {
            var response = await HttpClient.PostAsync(url, new FormUrlEncodedContent(requestBody));

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Console.Out.WriteLineAsync($"[ERROR] - GOG API returned {response.StatusCode}: {error}");
                return false;
            }

            response.EnsureSuccessStatusCode();
            _tokens = await response.Content.ReadFromJsonAsync<GogTokenResponse>() ?? new GogTokenResponse();
        }
        catch
        {
            // ignored
        }

        return _tokens.AccessToken is not null && _tokens.RefreshToken is not null &&
               !string.IsNullOrEmpty(_tokens.AccessToken) && !string.IsNullOrEmpty(_tokens.RefreshToken);
    }

    private static async Task<bool> RequestUserInformation()
    {
        const string url = "https://embed.gog.com/userData.json";
        
        await Console.Out.WriteLineAsync($"[INFO] - Fetching the user information...");
        await Console.Out.WriteLineAsync($"[INFO] - Sending request to {url}...");
        
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
            
            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            _userData = await response.Content.ReadFromJsonAsync<GogUserData>() ?? new GogUserData();
        }
        catch
        {
            // ignored
        }

        return _userData.UserId is not null && _userData.Username is not null && 
               !string.IsNullOrEmpty(_userData.UserId) && !string.IsNullOrEmpty(_userData.Username);
    }

    private static async Task<bool> SaveRefreshTokenToKeyring()
    {
        if (_userData.Username is null || string.IsNullOrEmpty(_userData.UserId) || 
            _tokens.RefreshToken is null || string.IsNullOrEmpty(_tokens.RefreshToken))
            return false;

        var saved = false;
        
        await Console.Out.WriteLineAsync($"[INFO] - Saving the user information on the system keyring...");
        
        try
        {
            saved = await SecretService.SaveRefreshTokenAsync(_userData.Username, _tokens.RefreshToken);
        }
        catch
        {
            // ignored
        }
        
        return saved;
    }
    
    // Properties:
    public static bool IsLoggedIn { get; private set; } = false;
}