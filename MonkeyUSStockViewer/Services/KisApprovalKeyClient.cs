using System.Net.Http;
using System.Text;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisApprovalKeyClient
{
    private readonly HttpClient _httpClient;

    public KisApprovalKeyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetApprovalKeyAsync(KisSettings settings, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            grant_type = "client_credentials",
            appkey = settings.AppKey,
            secretkey = settings.AppSecret
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(settings.ApprovalUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Approval request failed. Status={(int)response.StatusCode} {response.ReasonPhrase}, Body={responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("approval_key", out var approvalKeyElement))
        {
            throw new InvalidOperationException($"Approval response does not contain approval_key. Body={responseBody}");
        }

        var approvalKey = approvalKeyElement.GetString();
        if (string.IsNullOrWhiteSpace(approvalKey))
        {
            throw new InvalidOperationException($"Approval response contains an empty approval_key. Body={responseBody}");
        }

        return approvalKey;
    }
}
