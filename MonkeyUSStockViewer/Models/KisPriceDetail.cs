namespace MonkeyUSStockViewer.Models;

public sealed class KisPriceDetail
{
    public string QueryExchangeCode { get; init; } = string.Empty;

    public string QuerySymbol { get; init; } = string.Empty;

    public string RealtimeSymbol { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public string Open { get; init; } = string.Empty;

    public string High { get; init; } = string.Empty;

    public string Low { get; init; } = string.Empty;

    public string Last { get; init; } = string.Empty;

    public string Base { get; init; } = string.Empty;

    public string Volume { get; init; } = string.Empty;

    public string Amount { get; init; } = string.Empty;

    public string WonPrice { get; init; } = string.Empty;

    public string WonRate { get; init; } = string.Empty;

    public string ChangeRate { get; init; } = string.Empty;

    public string ExchangeRate { get; init; } = string.Empty;

    public string Orderable { get; init; } = string.Empty;

    public string Sector { get; init; } = string.Empty;

    public string RawJson { get; init; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
}
