namespace MonkeyUSStockViewer.Models;

public sealed class KisParsedMessage
{
    public bool IsJson { get; init; }

    public bool IsRealtimeData { get; init; }

    public string TrId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public KisRealtimeQuote? Quote { get; init; }
}
