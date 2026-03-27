using System.Text.Json;
using PhotoDrop.Models;

namespace PhotoDrop.Services;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly EventStorage _eventStorage;

    public TokenService(IConfiguration config, HttpClient httpClient, EventStorage eventStorage)
    {
        _config = config;
        _httpClient = httpClient;
        _eventStorage = eventStorage;
    }

    // Returns a valid access token for the event, refreshing it if expired.
    public async Task<string?> GetValidAccessTokenAsync(EventRecord eventRecord)
    {
        // If token hasn't expired yet, return it as-is
        if (eventRecord.TokenExpiresAt.HasValue && eventRecord.TokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return eventRecord.AccessToken;
        }

        // Token is expired or about to expire — refresh it
        if (string.IsNullOrWhiteSpace(eventRecord.RefreshToken))
        {
            return null; // No refresh token, can't get a new access token
        }

        var newToken = await RefreshAccessTokenAsync(eventRecord.RefreshToken);
        if (newToken is null)
        {
            return null; // Refresh failed
        }

        // Save the new token and expiry to the database
        eventRecord.AccessToken = newToken.AccessToken;
        eventRecord.TokenExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresInSeconds);
        _eventStorage.Update(eventRecord);

        return newToken.AccessToken;
    }

    // Calls Google's token endpoint to exchange a refresh token for a new access token.
    private async Task<TokenResponse?> RefreshAccessTokenAsync(string refreshToken)
    {
        var clientId = _config["Authentication:Google:ClientId"];
        var clientSecret = _config["Authentication:Google:ClientSecret"];

        var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId! },
            { "client_secret", clientSecret! },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", requestBody);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GoogleTokenResult>(json);

        if (result is null || string.IsNullOrWhiteSpace(result.access_token))
            return null;

        return new TokenResponse(result.access_token, result.expires_in);
    }

    private record TokenResponse(string AccessToken, int ExpiresInSeconds);
    private record GoogleTokenResult(string access_token, int expires_in);
}
