using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace MonkeyUSStockViewer.Models;

public sealed class TickerPriceRow : INotifyPropertyChanged
{
    private string _displayName = string.Empty;
    private string _symbol = string.Empty;
    private string _exchangeCode = string.Empty;
    private string _last = "-";
    private string _currency = string.Empty;
    private string _base = string.Empty;
    private string _changeRate = string.Empty;
    private string _wonPrice = string.Empty;
    private string _wonChange = string.Empty;
    private string _wonRate = string.Empty;
    private string _exchangeRate = string.Empty;
    private string _status = "Waiting";
    private string _updatedAt = "-";
    private bool _showKrw;
    private decimal _holdingQuantity;
    private decimal _averagePrice;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string Symbol
    {
        get => _symbol;
        set => SetField(ref _symbol, value);
    }

    public string ExchangeCode
    {
        get => _exchangeCode;
        set => SetField(ref _exchangeCode, value);
    }

    public string Last
    {
        get => _last;
        set => SetField(ref _last, value);
    }

    public string Currency
    {
        get => _currency;
        set => SetField(ref _currency, value);
    }

    public string Base
    {
        get => _base;
        set => SetField(ref _base, value);
    }

    public string ChangeRate
    {
        get => _changeRate;
        set => SetField(ref _changeRate, value);
    }

    public string WonPrice
    {
        get => _wonPrice;
        set => SetField(ref _wonPrice, value);
    }

    public string WonChange
    {
        get => _wonChange;
        set => SetField(ref _wonChange, value);
    }

    public string WonRate
    {
        get => _wonRate;
        set => SetField(ref _wonRate, value);
    }

    public string ExchangeRate
    {
        get => _exchangeRate;
        set => SetField(ref _exchangeRate, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string UpdatedAt
    {
        get => _updatedAt;
        set => SetField(ref _updatedAt, value);
    }

    public bool ShowKrw
    {
        get => _showKrw;
        set => SetBoolField(ref _showKrw, value);
    }

    public decimal HoldingQuantity
    {
        get => _holdingQuantity;
        set => SetDecimalField(ref _holdingQuantity, value);
    }

    public decimal AveragePrice
    {
        get => _averagePrice;
        set => SetDecimalField(ref _averagePrice, value);
    }

    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Symbol : DisplayName;

    public string PriceText => DisplayPriceText;

    public string CompactPriceText => DisplayPriceText;

    private string DisplayPriceText
    {
        get
        {
            if (ShowKrw && HasWonPrice)
            {
                return $"{FormatKrwPrice(WonPrice)}¿ø";
            }

            return $"${FormatUsdPrice(Last)}";
        }
    }

    public string MetaText => $"{Symbol} / {ExchangeCode} / {UpdatedAt}";

    public string SymbolText => $"{Symbol} / {ExchangeCode}";

    public string CompactChangeText
    {
        get
        {
            if (Status.Equals("ERR", StringComparison.OrdinalIgnoreCase)
                || Status.Equals("NO DATA", StringComparison.OrdinalIgnoreCase))
            {
                return Status;
            }

            var changeText = ChangeText;
            if (string.IsNullOrWhiteSpace(changeText))
            {
                return "-";
            }

            return changeText;
        }
    }

    public string ChangeText
    {
        get
        {
            if (ShowKrw && HasWonPrice)
            {
                var changeText = GetWonChangeText();
                var rateText = GetDisplayRate();
                return string.IsNullOrWhiteSpace(rateText) ? changeText : $"{changeText} ({rateText})";
            }

            if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
            {
                return "-";
            }

            var change = last - basePrice;
            var usdRateText = FormatRate(ChangeRate, basePrice, change);
            return $"{change:+0.00;-0.00;0.00} ({usdRateText})";
        }
    }

    public string ChangeBrush
    {
        get
        {
            if (ShowKrw && HasWonPrice)
            {
                var sign = GetRateSign(WonRate);
                if (sign == 0)
                {
                    sign = GetUsdChangeSign();
                }

                return GetBrushBySign(sign);
            }

            if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
            {
                return "#666666";
            }

            var change = last - basePrice;
            return GetBrushBySign(decimal.Sign(change));
        }
    }

    public string CompactBrush
    {
        get
        {
            if (Status.Equals("ERR", StringComparison.OrdinalIgnoreCase)
                || Status.Equals("NO DATA", StringComparison.OrdinalIgnoreCase))
            {
                return StatusBrush;
            }

            return ChangeBrush;
        }
    }

    public string StatusBrush => Status.ToUpperInvariant() switch
    {
        "ERR" => "#CC1F1A",
        "NO DATA" => "#B45309",
        _ => "#666666"
    };

    public string HoldingTooltip
    {
        get
        {
            if (HoldingQuantity <= 0 || AveragePrice <= 0)
            {
                return "No holding data";
            }

            if (!TryParseDecimal(Last, out var last))
            {
                return "Holding data\nPrice unavailable";
            }

            var displayLast = last;
            var displayAverage = AveragePrice;
            var currency = "USD";

            if (ShowKrw && HasWonPrice && TryParseDecimal(WonPrice, out var wonLast))
            {
                displayLast = wonLast;
                currency = "KRW";

                if (TryParseDecimal(ExchangeRate, out var exchangeRate))
                {
                    displayAverage = AveragePrice * exchangeRate;
                }
            }

            var cost = HoldingQuantity * displayAverage;
            var value = HoldingQuantity * displayLast;
            var profit = value - cost;
            var profitRate = cost == 0 ? 0 : profit / cost * 100;
            var averageText = currency == "KRW" ? FormatKrwPrice(displayAverage) : $"{displayAverage:0.00}";
            var valueText = currency == "KRW" ? FormatKrwPrice(value) : $"{value:0.00}";
            var profitText = currency == "KRW" ? FormatSignedKrw(profit) : $"{profit:+0.00;-0.00;0.00}";

            return $"Holding\nQty: {HoldingQuantity:0.####}\nAvg: {averageText} {currency}\nValue: {valueText} {currency}\nP/L: {profitText} {currency} ({profitRate:+0.00;-0.00;0.00}%)";
        }
    }

    private bool HasWonPrice => TryParseDecimal(WonPrice, out _);

    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(CompactPriceText));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(SymbolText));
        OnPropertyChanged(nameof(CompactChangeText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(ChangeBrush));
        OnPropertyChanged(nameof(CompactBrush));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(HoldingTooltip));
    }

    public void ToggleDisplayCurrency()
    {
        ShowKrw = !ShowKrw;
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RefreshComputed();
    }

    private void SetDecimalField(ref decimal field, decimal value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RefreshComputed();
    }

    private void SetBoolField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RefreshComputed();
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static string FormatUsdPrice(string value)
    {
        return TryParseDecimal(value, out var parsed) ? $"{parsed:0.00}" : value;
    }

    private static string FormatKrwPrice(string value)
    {
        return TryParseDecimal(value, out var parsed) ? FormatKrwPrice(parsed) : value;
    }

    private static string FormatKrwPrice(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedKrw(decimal value)
    {
        var formatted = Math.Abs(value).ToString("N0", CultureInfo.InvariantCulture);
        return value switch
        {
            > 0 => $"+{formatted}",
            < 0 => $"-{formatted}",
            _ => "0"
        };
    }

    private string GetWonChangeText()
    {
        if (!TryParseDecimal(WonChange, out var change))
        {
            return "-";
        }

        var sign = GetRateSign(WonRate);
        if (sign == 0)
        {
            sign = GetUsdChangeSign();
        }

        if (sign == 0)
        {
            sign = decimal.Sign(change);
        }

        var signedChange = Math.Abs(change) * sign;
        return FormatSignedKrw(signedChange);
    }

    private string GetDisplayRate()
    {
        if (ShowKrw && TryParseDecimal(WonRate, out var wonRate))
        {
            return $"{wonRate:+0.00;-0.00;0.00}%";
        }

        if (TryParseDecimal(ChangeRate, out var parsedRate))
        {
            return $"{parsedRate:+0.00;-0.00;0.00}%";
        }

        if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
        {
            return string.Empty;
        }

        if (basePrice == 0)
        {
            return "0.00%";
        }

        var change = last - basePrice;
        var calculatedRate = change / basePrice * 100;
        return $"{calculatedRate:+0.00;-0.00;0.00}%";
    }

    private int GetUsdChangeSign()
    {
        if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
        {
            return 0;
        }

        return decimal.Sign(last - basePrice);
    }

    private static int GetRateSign(string rate)
    {
        return TryParseDecimal(rate, out var parsedRate) ? decimal.Sign(parsedRate) : 0;
    }

    private static string GetBrushBySign(int sign)
    {
        return sign switch
        {
            > 0 => "#CC1F1A",
            < 0 => "#2563EB",
            _ => "#666666"
        };
    }

    private static string FormatRate(string apiRate, decimal basePrice, decimal change)
    {
        if (TryParseDecimal(apiRate, out var parsedRate))
        {
            return $"{parsedRate:+0.00;-0.00;0.00}%";
        }

        if (basePrice == 0)
        {
            return "0.00%";
        }

        var calculatedRate = change / basePrice * 100;
        return $"{calculatedRate:+0.00;-0.00;0.00}%";
    }
}
