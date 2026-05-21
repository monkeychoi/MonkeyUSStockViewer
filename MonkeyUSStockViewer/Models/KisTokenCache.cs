namespace MonkeyUSStockViewer.Models;

public sealed class KisTokenCache
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public string AppKeyMask { get; set; } = string.Empty;

    public bool IsUsableFor(string appKey)
    {
        return !string.IsNullOrWhiteSpace(AccessToken)
            && ExpiresAt > DateTimeOffset.Now.AddMinutes(5)
            && IsForAppKey(appKey);
    }

    public bool IsValidFor(string appKey)
    {
        return !string.IsNullOrWhiteSpace(AccessToken)
            && ExpiresAt > DateTimeOffset.Now
            && IsForAppKey(appKey);
    }

    public bool IsForAppKey(string appKey)
    {
        return string.Equals(AppKeyMask, CreateAppKeyMask(appKey), StringComparison.Ordinal);
    }

    public static string CreateAppKeyMask(string appKey)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return string.Empty;
        }

        return appKey.Length <= 8 ? appKey : $"{appKey[..4]}...{appKey[^4..]}";
    }
}
