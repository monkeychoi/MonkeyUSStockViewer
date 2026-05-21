namespace MonkeyUSStockViewer.Models;

public sealed class KisStockSetting
{
    public string DisplayName { get; set; } = "NVIDIA";

    public string Symbol { get; set; } = "NVDA";

    public string Market { get; set; } = "NASDAQ";

    public string ExchangeMode { get; set; } = "Auto";

    public string DayExchangeCode { get; set; } = "BAQ";

    public string RegularExchangeCode { get; set; } = "NAS";

    public string ManualExchangeCode { get; set; } = "BAQ";

    public decimal HoldingQuantity { get; set; }

    public decimal AveragePrice { get; set; }

    public string ResolveExchangeCode()
    {
        return ExchangeMode.Trim().ToUpperInvariant() switch
        {
            "AUTO" => IsRegularSession(DateTime.Now) ? RegularExchangeCode : DayExchangeCode,
            "REGULAR" => RegularExchangeCode,
            "MANUAL" => ManualExchangeCode,
            _ => DayExchangeCode
        };
    }

    public override string ToString()
    {
        var name = string.IsNullOrWhiteSpace(DisplayName) ? Symbol : DisplayName;
        return ExchangeMode.Trim().ToUpperInvariant() switch
        {
            "AUTO" => $"{name} ({Symbol}) - Auto [{DayExchangeCode}/{RegularExchangeCode}]",
            "REGULAR" => $"{name} ({Symbol}) - Regular [{RegularExchangeCode}]",
            "MANUAL" => $"{name} ({Symbol}) - Manual [{ManualExchangeCode}]",
            _ => $"{name} ({Symbol}) - Day [{DayExchangeCode}]"
        };
    }

    private static bool IsRegularSession(DateTime now)
    {
        var time = now.TimeOfDay;
        return time >= TimeSpan.FromHours(17) || time < TimeSpan.FromHours(9);
    }
}
