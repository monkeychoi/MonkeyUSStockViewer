using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisAccessTokenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _tokenCachePath;
    private KisAccessToken? _cachedToken;

    public KisAccessTokenClient(HttpClient httpClient, string tokenCachePath)
    {
        _httpClient = httpClient;
        _tokenCachePath = tokenCachePath;
    }

    public async Task<KisAccessToken> GetTokenAsync(KisSettings settings, CancellationToken cancellationToken)
    {
        if (_cachedToken?.IsUsable == true)
        {
            return _cachedToken;
        }

        var fileToken = LoadTokenFromFile(settings.AppKey);
        if (fileToken?.IsUsable == true)
        {
            _cachedToken = fileToken;
            return _cachedToken;
        }

        var requestBody = new
        {
            grant_type = "client_credentials",
            appkey = settings.AppKey,
            appsecret = settings.AppSecret
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(settings.AccessTokenUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (fileToken is not null && fileToken.ExpiresAt > DateTimeOffset.Now)
            {
                _cachedToken = fileToken;
                return _cachedToken;
            }

            throw new InvalidOperationException(
                $"Access token request failed. Status={(int)response.StatusCode} {response.ReasonPhrase}, Body={responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var accessToken = GetString(root, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException($"Token response does not contain access_token. Body={responseBody}");
        }

        _cachedToken = new KisAccessToken
        {
            AccessToken = accessToken,
            ExpiresAt = GetExpiresAt(root)
        };

        SaveTokenToFile(settings.AppKey, _cachedToken);

        return _cachedToken;
    }

    private KisAccessToken? LoadTokenFromFile(string appKey)
    {
        if (!File.Exists(_tokenCachePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_tokenCachePath);
            var cache = JsonSerializer.Deserialize<KisTokenCache>(json, JsonOptions);
            if (cache?.IsValidFor(appKey) != true)
            {
                return null;
            }

            return new KisAccessToken
            {
                AccessToken = cache.AccessToken,
                ExpiresAt = cache.ExpiresAt
            };
        }
        catch
        {
            return null;
        }
    }

    private void SaveTokenToFile(string appKey, KisAccessToken token)
    {
        var cache = new KisTokenCache
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            AppKeyMask = KisTokenCache.CreateAppKeyMask(appKey)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_tokenCachePath) ?? ".");
        var json = JsonSerializer.Serialize(cache, JsonOptions);
        File.WriteAllText(_tokenCachePath, json);
    }

    private static DateTimeOffset GetExpiresAt(JsonElement root)
    {
        var expiredText = GetString(root, "access_token_token_expired");
        if (DateTimeOffset.TryParse(expiredText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        var expiresInText = GetString(root, "expires_in");
        if (int.TryParse(expiresInText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresInSeconds)
            && expiresInSeconds > 0)
        {
            return DateTimeOffset.Now.AddSeconds(expiresInSeconds);
        }

        return DateTimeOffset.Now.AddHours(23);
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }
}
