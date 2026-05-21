using System.Net.Http;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisPriceDetailClient
{
    private const string TrId = "HHDFS76200200";

    private readonly HttpClient _httpClient;

    public KisPriceDetailClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<KisPriceDetail> GetPriceDetailAsync(
        KisSettings settings,
        string accessToken,
        CancellationToken cancellationToken)
    {
        return await GetPriceDetailAsync(
            settings,
            settings.ExchangeCode,
            settings.Symbol,
            accessToken,
            cancellationToken);
    }

    public async Task<KisPriceDetail> GetPriceDetailAsync(
        KisSettings settings,
        string exchangeCode,
        string symbol,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(settings.PriceDetailUrl, exchangeCode, symbol);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("authorization", $"Bearer {accessToken}");
        request.Headers.TryAddWithoutValidation("appkey", settings.AppKey);
        request.Headers.TryAddWithoutValidation("appsecret", settings.AppSecret);
        request.Headers.TryAddWithoutValidation("tr_id", TrId);
        request.Headers.TryAddWithoutValidation("custtype", "P");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Price detail request failed. Status={(int)response.StatusCode} {response.ReasonPhrase}, Body={responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var rtCd = GetString(root, "rt_cd");
        if (rtCd != "0")
        {
            var msgCd = GetString(root, "msg_cd");
            var msg1 = GetString(root, "msg1");
            throw new InvalidOperationException($"Price detail API error. rt_cd={rtCd}, msg_cd={msgCd}, msg1={msg1}");
        }

        if (!root.TryGetProperty("output", out var output))
        {
            throw new InvalidOperationException($"Price detail response does not contain output. Body={responseBody}");
        }

        return new KisPriceDetail
        {
            QueryExchangeCode = exchangeCode,
            QuerySymbol = symbol,
            RealtimeSymbol = GetString(output, "rsym"),
            Currency = GetString(output, "curr"),
            Open = GetString(output, "open"),
            High = GetString(output, "high"),
            Low = GetString(output, "low"),
            Last = GetString(output, "last"),
            Base = GetString(output, "base"),
            Volume = GetString(output, "tvol"),
            Amount = GetString(output, "tamt"),
            WonPrice = GetString(output, "t_xprc"),
            WonRate = GetString(output, "t_xrat"),
            ChangeRate = GetString(output, "t_xrat"),
            ExchangeRate = GetString(output, "t_rate"),
            Orderable = GetString(output, "e_ordyn"),
            Sector = GetString(output, "e_icod"),
            RawJson = responseBody,
            ReceivedAt = DateTimeOffset.Now
        };
    }

    private static Uri BuildUri(string priceDetailUrl, string exchangeCode, string symbol)
    {
        var builder = new UriBuilder(priceDetailUrl);
        var query = $"AUTH=&EXCD={Uri.EscapeDataString(exchangeCode)}&SYMB={Uri.EscapeDataString(symbol)}";
        builder.Query = query;
        return builder.Uri;
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
