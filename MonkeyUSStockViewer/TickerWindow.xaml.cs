using System.Collections.ObjectModel;
using System.Windows;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer
{
    public partial class TickerWindow : Window
    {
        private readonly Dictionary<string, TickerPriceRow> _rowsByKey = new(StringComparer.OrdinalIgnoreCase);
        private bool _allowClose;

        public event EventHandler? SettingsRequested;

        public event EventHandler<bool>? TopmostChanged;

        public ObservableCollection<TickerPriceRow> Rows { get; } = new();

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
            TopmostCheckBox.IsChecked = settings.AlwaysOnTop;
            FontSize = settings.TickerFontSize;
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
                    Status = "Waiting",
                    UpdatedAt = "-"
                };

                Rows.Add(row);
                _rowsByKey[BuildKey(stock.Symbol, exchangeCode)] = row;
            }
        }

        public void UpdatePrice(KisStockSetting stock, string exchangeCode, KisPriceDetail price)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol, exchangeCode), out var row))
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
            row.Status = "OK";
            row.UpdatedAt = price.ReceivedAt.ToString("HH:mm:ss");
        }

        public void MarkNoData(KisStockSetting stock, string exchangeCode)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol, exchangeCode), out var row))
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
            row.Status = "NO DATA";
            row.UpdatedAt = DateTime.Now.ToString("HH:mm:ss");
        }

        public void MarkError(KisStockSetting stock, string exchangeCode, string message)
        {
            if (!_rowsByKey.TryGetValue(BuildKey(stock.Symbol, exchangeCode), out var row))
            {
                return;
            }

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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TopmostCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostCheckBox.IsChecked == true;
            TopmostChanged?.Invoke(this, Topmost);
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

        private static string BuildKey(string symbol, string exchangeCode)
        {
            return $"{symbol.Trim().ToUpperInvariant()}|{exchangeCode.Trim().ToUpperInvariant()}";
        }
    }
}
