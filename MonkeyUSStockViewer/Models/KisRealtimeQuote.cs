namespace MonkeyUSStockViewer.Models;

public sealed class KisRealtimeQuote
{
    public string Symbol { get; init; } = string.Empty;

    public string LastPrice { get; init; } = string.Empty;

    public string ExchangeTime { get; init; } = string.Empty;

    public string KoreaTime { get; init; } = string.Empty;

    public int FieldCount { get; init; }

    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();
}
