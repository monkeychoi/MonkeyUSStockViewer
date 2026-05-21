using System.IO;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisSettingsService
{
    public const string SettingsFileName = "kis_settings.json";
    public const string SampleFileName = "kis_settings.sample.json";
    public const string TokenCacheFileName = "kis_token_cache.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public KisSettings LoadOrCreate()
    {
        var settingsPath = GetSettingsPath();
        if (!File.Exists(settingsPath))
        {
            WriteSettingsFile(settingsPath, new KisSettings());
        }

        var json = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<KisSettings>(json, JsonOptions);

        settings ??= new KisSettings();
        Normalize(settings);

        return settings;
    }

    public string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
    }

    public string GetTokenCachePath()
    {
        return Path.Combine(AppContext.BaseDirectory, TokenCacheFileName);
    }

    public void EnsureSampleFile()
    {
        var samplePath = Path.Combine(FindProjectDirectory(), SampleFileName);
        if (!File.Exists(samplePath))
        {
            WriteSettingsFile(samplePath, new KisSettings());
        }
    }

    public void Save(KisSettings settings)
    {
        Normalize(settings);
        WriteSettingsFile(GetSettingsPath(), settings);
    }

    private static void WriteSettingsFile(string path, KisSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void Normalize(KisSettings settings)
    {
        settings.Stocks ??= new List<KisStockSetting>();
        settings.PollingIntervalSeconds = Math.Max(1, settings.PollingIntervalSeconds);
        settings.TickerFontSize = Math.Clamp(settings.TickerFontSize, 9, 32);
        settings.TickerWindowWidth = Math.Max(180, settings.TickerWindowWidth);
        settings.TickerWindowHeight = Math.Max(100, settings.TickerWindowHeight);

        if (settings.Stocks.Count == 0 && !string.IsNullOrWhiteSpace(settings.Symbol))
        {
            settings.Stocks.Add(new KisStockSetting
            {
                DisplayName = settings.Symbol,
                Symbol = settings.Symbol,
                DayExchangeCode = settings.ExchangeCode,
                ManualExchangeCode = settings.ExchangeCode
            });
        }

        foreach (var stock in settings.Stocks)
        {
            stock.DisplayName = stock.DisplayName.Trim();
            stock.Symbol = stock.Symbol.Trim().ToUpperInvariant();
            stock.Market = stock.Market.Trim();
            stock.ExchangeMode = string.IsNullOrWhiteSpace(stock.ExchangeMode) ? "Day" : stock.ExchangeMode.Trim();
            stock.DayExchangeCode = stock.DayExchangeCode.Trim().ToUpperInvariant();
            stock.RegularExchangeCode = stock.RegularExchangeCode.Trim().ToUpperInvariant();
            stock.ManualExchangeCode = stock.ManualExchangeCode.Trim().ToUpperInvariant();
            stock.HoldingQuantity = Math.Max(0m, stock.HoldingQuantity);
            stock.AveragePrice = Math.Max(0m, stock.AveragePrice);
        }
    }

    private static string FindProjectDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(baseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MonkeyUSStockViewer.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
