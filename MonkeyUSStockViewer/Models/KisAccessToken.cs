namespace MonkeyUSStockViewer.Models;

public sealed class KisAccessToken
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public bool IsUsable => !string.IsNullOrWhiteSpace(AccessToken)
        && ExpiresAt > DateTimeOffset.Now.AddMinutes(5);
}
