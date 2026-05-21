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
    private string _status = "Waiting";
    private string _updatedAt = "-";
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

    public string PriceText => string.IsNullOrWhiteSpace(Currency) ? Last : $"{Last} {Currency}";

    public string MetaText => $"{Symbol} / {ExchangeCode} / {UpdatedAt}";

    public string SymbolText => $"{Symbol} / {ExchangeCode}";

    public string ChangeText
    {
        get
        {
            if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
            {
                return "-";
            }

            var change = last - basePrice;
            var rateText = FormatRate(ChangeRate, basePrice, change);
            return $"{change:+0.00;-0.00;0.00} ({rateText})";
        }
    }

    public string ChangeBrush
    {
        get
        {
            if (!TryParseDecimal(Last, out var last) || !TryParseDecimal(Base, out var basePrice))
            {
                return "#666666";
            }

            var change = last - basePrice;
            if (change > 0)
            {
                return "#CC1F1A";
            }

            if (change < 0)
            {
                return "#2563EB";
            }

            return "#666666";
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

            var cost = HoldingQuantity * AveragePrice;
            var value = HoldingQuantity * last;
            var profit = value - cost;
            var rate = cost == 0 ? 0 : profit / cost * 100;
            var currency = string.IsNullOrWhiteSpace(Currency) ? "USD" : Currency;

            return $"Holding\nQty: {HoldingQuantity:0.####}\nAvg: {AveragePrice:0.00} {currency}\nValue: {value:0.00} {currency}\nP/L: {profit:+0.00;-0.00;0.00} {currency} ({rate:+0.00;-0.00;0.00}%)";
        }
    }

    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(SymbolText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(ChangeBrush));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(HoldingTooltip));
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

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
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
