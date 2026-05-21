namespace MonkeyUSStockViewer.Models;

public sealed class KisStockSetting
{
    public string DisplayName { get; set; } = "NVIDIA";

    public string Symbol { get; set; } = "NVDA";

    public string Market { get; set; } = "NASDAQ";

    public string ExchangeMode { get; set; } = "Day";

    public string DayExchangeCode { get; set; } = "BAQ";

    public string RegularExchangeCode { get; set; } = "NAS";

    public string ManualExchangeCode { get; set; } = "BAQ";

    public string ResolveExchangeCode()
    {
        return ExchangeMode.Trim().ToUpperInvariant() switch
        {
            "REGULAR" => RegularExchangeCode,
            "MANUAL" => ManualExchangeCode,
            _ => DayExchangeCode
        };
    }

    public override string ToString()
    {
        var name = string.IsNullOrWhiteSpace(DisplayName) ? Symbol : DisplayName;
        return $"{name} ({Symbol}) - {ResolveExchangeCode()}";
    }
}
