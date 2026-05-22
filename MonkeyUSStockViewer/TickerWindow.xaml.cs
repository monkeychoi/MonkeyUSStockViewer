using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer
{
    public partial class TickerWindow : Window, INotifyPropertyChanged
    {
        private readonly Dictionary<string, TickerPriceRow> _rowsByKey = new(StringComparer.OrdinalIgnoreCase);
        private bool _allowClose;
        private bool _compactMode = true;
        private double _subFontSize = 10;

        public event EventHandler? SettingsRequested;

        public event EventHandler<bool>? TopmostChanged;

        public event EventHandler<bool>? CompactModeChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TickerPriceRow> Rows { get; } = new();

        public bool CompactMode
        {
            get => _compactMode;
            private set
            {
                if (_compactMode == value)
                {
                    return;
                }

                _compactMode = value;
                OnPropertyChanged();
            }
        }

        public double SubFontSize
        {
            get => _subFontSize;
            private set
            {
                if (Math.Abs(_subFontSize - value) < 0.001)
                {
                    return;
                }

                _subFontSize = value;
                OnPropertyChanged();
            }
        }

        public TickerWindow(KisSettings settings)
        {
            InitializeComponent();
            DataContext = this;
            ApplySettings(settings);
            SetStocks(settings.Stocks);
        }

        public void ApplySettings(KisSettings settings)
        {
            Topmost = settings.AlwaysOnTop;
            CompactMode = settings.CompactMode;
            FontSize = settings.TickerFontSize;
            SubFontSize = Math.Max(9, settings.TickerFontSize * 0.78);
            Left = settings.TickerWindowLeft;
            Top = settings.TickerWindowTop;
            Width = settings.TickerWindowWidth;
            Height = settings.TickerWindowHeight;
        }

        public void SetStocks(IEnumerable<KisStockSetting> stocks)
        {
            Rows.Clear();
            _rowsByKey.Clear();

            foreach (var stock in stocks)
            {
                var exchangeCode = stock.ResolveExchangeCode();
                var row = new TickerPriceRow
                {
                    DisplayName = stock.DisplayName,
                    Symbol = stock.Symbol,
                    ExchangeCode = exchangeCode,
                    Last = "-",
                    Currency = string.Empty,
                    Base = string.Empty,
                    ChangeRate = string.Empty,
                    WonPrice = string.Empty,
                    WonChange = string.Empty,
                    WonRate = string.Empty,
                    ExchangeRate = string.Empty,
                    HoldingQuantity = stock.HoldingQuantity,
                    AveragePrice = stock.AveragePrice,
                    Status = "Waiting",
                    UpdatedAt = "-"
                };

                Rows.Add(row);
                _rowsByKey[BuildKey(stock.Symbol)] = row;
            }
        }

        public void UpdatePrice(KisStockSetting stock, string exchangeCode, KisPriceDetail price)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol), out var row))
            {
                return;
            }

            row.DisplayName = stock.DisplayName;
            row.Symbol = stock.Symbol;
            row.ExchangeCode = exchangeCode;
            row.Last = string.IsNullOrWhiteSpace(price.Last) ? "-" : price.Last;
            row.Currency = price.Currency;
            row.Base = price.Base;
            row.ChangeRate = price.ChangeRate;
            row.WonPrice = price.WonPrice;
            row.WonChange = price.WonChange;
            row.WonRate = price.WonRate;
            row.ExchangeRate = price.ExchangeRate;
            row.HoldingQuantity = stock.HoldingQuantity;
            row.AveragePrice = stock.AveragePrice;
            row.Status = "OK";
            row.UpdatedAt = price.ReceivedAt.ToString("HH:mm:ss");
        }

        public void MarkNoData(KisStockSetting stock, string exchangeCode)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol), out var row))
            {
                return;
            }

            row.DisplayName = stock.DisplayName;
            row.Symbol = stock.Symbol;
            row.ExchangeCode = exchangeCode;
            row.Last = "-";
            row.Currency = string.Empty;
            row.Base = string.Empty;
            row.ChangeRate = string.Empty;
            row.WonPrice = string.Empty;
            row.WonChange = string.Empty;
            row.WonRate = string.Empty;
            row.ExchangeRate = string.Empty;
            row.HoldingQuantity = stock.HoldingQuantity;
            row.AveragePrice = stock.AveragePrice;
            row.Status = "NO DATA";
            row.UpdatedAt = DateTime.Now.ToString("HH:mm:ss");
        }

        public void MarkError(KisStockSetting stock, string exchangeCode, string message)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol), out var row))
            {
                return;
            }

            row.ExchangeCode = exchangeCode;
            row.HoldingQuantity = stock.HoldingQuantity;
            row.AveragePrice = stock.AveragePrice;
            row.Status = string.IsNullOrWhiteSpace(message) ? "ERR" : "ERR";
            row.UpdatedAt = DateTime.Now.ToString("HH:mm:ss");
        }

        public void SaveBoundsTo(KisSettings settings)
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            settings.TickerWindowLeft = Left;
            settings.TickerWindowTop = Top;
            settings.TickerWindowWidth = Width;
            settings.TickerWindowHeight = Height;
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        private void TickerContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            CompactModeMenuItem.IsChecked = CompactMode;
            TopmostMenuItem.IsChecked = Topmost;
        }

        private void CompactModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CompactMode = CompactModeMenuItem.IsChecked;
            CompactModeChanged?.Invoke(this, CompactMode);
        }

        private void TopmostMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostMenuItem.IsChecked;
            TopmostChanged?.Invoke(this, Topmost);
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PriceListViewItem_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
            {
                return;
            }

            if (sender is FrameworkElement { DataContext: TickerPriceRow row })
            {
                row.ToggleDisplayCurrency();
            }
        }

        private void Window_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            SettingsRequested?.Invoke(this, EventArgs.Empty);
            e.Cancel = true;
        }

        private static string BuildKey(string symbol)
        {
            return symbol.Trim().ToUpperInvariant();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
