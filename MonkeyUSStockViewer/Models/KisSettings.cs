namespace MonkeyUSStockViewer.Models;

public sealed class KisSettings
{
    public string AccessTokenUrl { get; set; } = "https://openapi.koreainvestment.com:9443/oauth2/tokenP";

    public string PriceDetailUrl { get; set; } = "https://openapi.koreainvestment.com:9443/uapi/overseas-price/v1/quotations/price-detail";

    public string AppKey { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string ExchangeCode { get; set; } = "BAQ";

    public string Symbol { get; set; } = "NVDA";

    public int PollingIntervalSeconds { get; set; } = 2;

    public double TickerFontSize { get; set; } = 13;

    public bool AlwaysOnTop { get; set; } = true;

    public double TickerWindowLeft { get; set; } = 100;

    public double TickerWindowTop { get; set; } = 100;

    public double TickerWindowWidth { get; set; } = 280;

    public double TickerWindowHeight { get; set; } = 180;

    public List<KisStockSetting> Stocks { get; set; } = new()
    {
        new KisStockSetting()
    };
}
