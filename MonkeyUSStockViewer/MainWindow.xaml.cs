using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MonkeyUSStockViewer.Models;
using MonkeyUSStockViewer.Services;

namespace MonkeyUSStockViewer
{
    public partial class MainWindow : Window
    {
        private const int MaxLogLines = 700;

        private readonly KisSettingsService _settingsService = new();
        private readonly HttpClient _httpClient = new();
        private readonly List<string> _logLines = new();
        private readonly List<KisStockSetting> _stocks = new();

        private KisAccessTokenClient _accessTokenClient;
        private KisPriceDetailClient _priceDetailClient;
        private KisSettings _settings = new();
        private CancellationTokenSource? _pollingCts;
        private Task? _pollingTask;
        private TickerWindow? _tickerWindow;
        private bool _isClosing;
        private bool _isLoadingStockEditor;

        public MainWindow()
        {
            InitializeComponent();
            _accessTokenClient = new KisAccessTokenClient(_httpClient, _settingsService.GetTokenCachePath());
            _priceDetailClient = new KisPriceDetailClient(_httpClient);
            LoadInitialSettings();
        }

        private void LoadInitialSettings()
        {
            try
            {
                _settingsService.EnsureSampleFile();
                _settings = _settingsService.LoadOrCreate();
                ApplySettingsToUi(_settings);
                AddLog($"Settings path: {_settingsService.GetSettingsPath()}");
                AddLog($"Loaded settings: app_key={Mask(_settings.AppKey)}, stocks={_stocks.Count}");
            }
            catch (Exception ex)
            {
                AddLog($"Settings load failed: {ex.Message}");
                StatusTextBlock.Text = "Settings error";
            }
        }

        private void ApplySettingsToUi(KisSettings settings)
        {
            AppKeyTextBox.Text = settings.AppKey;
            AppSecretPasswordBox.Password = settings.AppSecret;
            AccessTokenUrlTextBox.Text = settings.AccessTokenUrl;
            PriceDetailUrlTextBox.Text = settings.PriceDetailUrl;
            PollingIntervalTextBox.Text = settings.PollingIntervalSeconds.ToString();
            TickerFontSizeTextBox.Text = settings.TickerFontSize.ToString("0");
            AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;

            _stocks.Clear();
            _stocks.AddRange(settings.Stocks.Select(CloneStock));
            RefreshStocksList();

            if (_stocks.Count > 0)
            {
                StocksListBox.SelectedIndex = 0;
            }
            else
            {
                ClearStockEditor();
            }
        }

        private async void StartPollingButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPollingAsync();
        }

        private async void StopPollingButton_Click(object sender, RoutedEventArgs e)
        {
            await StopPollingAsync();
            StatusTextBlock.Text = "Stopped";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings = BuildSettingsFromUi();
                ValidateSettings(_settings);
                SaveTickerBounds();
                _settingsService.Save(_settings);
                StatusTextBlock.Text = "Saved";
                AddLog("Settings saved.");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Save failed";
                AddLog($"Save failed: {ex.Message}");
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            await StopPollingAsync();
            SaveTickerBounds();
            _tickerWindow?.ForceClose();
            _httpClient.Dispose();
        }

        private async Task StartPollingAsync()
        {
            try
            {
                SetBusyUi(true);
                StatusTextBlock.Text = "Starting";

                await StopPollingAsync();

                _settings = BuildSettingsFromUi();
                ValidateSettings(_settings);
                SaveTickerBounds();
                _settingsService.Save(_settings);

                _pollingCts = new CancellationTokenSource();
                var token = _pollingCts.Token;

                AddLog($"Requesting access token: {Mask(_settings.AppKey)}");
                var accessToken = await _accessTokenClient.GetTokenAsync(_settings, token);
                AccessTokenTextBox.Text = $"{Mask(accessToken.AccessToken)} / expires {accessToken.ExpiresAt:yyyy-MM-dd HH:mm:ss}";
                AddLog($"Access token ready. ExpiresAt={accessToken.ExpiresAt:yyyy-MM-dd HH:mm:ss}");

                ShowTickerWindow();
                _pollingTask = PollingLoopAsync(token);

                SetPollingUi(true);
                StatusTextBlock.Text = "Polling";
                Hide();
            }
            catch (Exception ex)
            {
                AddLog($"Start polling failed: {ex.Message}");
                StatusTextBlock.Text = "Start failed";
                await StopPollingAsync();
            }
            finally
            {
                SetBusyUi(false);
            }
        }

        private async Task StopPollingAsync()
        {
            var cts = _pollingCts;
            _pollingCts = null;

            if (cts is not null)
            {
                cts.Cancel();
                try
                {
                    if (_pollingTask is not null)
                    {
                        await _pollingTask;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    cts.Dispose();
                    _pollingTask = null;
                }
            }

            SetPollingUi(false);
        }

        private async Task PollingLoopAsync(CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.PollingIntervalSeconds));
            var stocks = _settings.Stocks.Select(CloneStock).ToList();

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var stock in stocks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var exchangeCode = stock.ResolveExchangeCode();
                    try
                    {
                        var accessToken = await _accessTokenClient.GetTokenAsync(_settings, cancellationToken);
                        var price = await _priceDetailClient.GetPriceDetailAsync(
                            _settings,
                            exchangeCode,
                            stock.Symbol,
                            accessToken.AccessToken,
                            cancellationToken);

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (HasPriceData(price))
                            {
                                _tickerWindow?.UpdatePrice(stock, exchangeCode, price);
                            }
                            else
                            {
                                _tickerWindow?.MarkNoData(stock, exchangeCode);
                            }

                            if (LogRawJsonCheckBox.IsChecked == true)
                            {
                                AddLog(price.RawJson);
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _tickerWindow?.MarkError(stock, exchangeCode, ex.Message);
                            AddLog($"Polling error {stock.Symbol}/{exchangeCode}: {ex.Message}");
                        });
                    }
                }

                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void ShowTickerWindow()
        {
            if (_tickerWindow is null)
            {
                _tickerWindow = new TickerWindow(_settings);
                _tickerWindow.SettingsRequested += TickerWindow_SettingsRequested;
                _tickerWindow.TopmostChanged += TickerWindow_TopmostChanged;
                _tickerWindow.CompactModeChanged += TickerWindow_CompactModeChanged;
            }
            else
            {
                _tickerWindow.ApplySettings(_settings);
                _tickerWindow.SetStocks(_settings.Stocks);
            }

            _tickerWindow.Show();
            _tickerWindow.Activate();
        }

        private static bool HasPriceData(KisPriceDetail price)
        {
            return !string.IsNullOrWhiteSpace(price.RealtimeSymbol)
                && !string.IsNullOrWhiteSpace(price.Last);
        }

        private void TickerWindow_TopmostChanged(object? sender, bool isTopmost)
        {
            _settings.AlwaysOnTop = isTopmost;
            AlwaysOnTopCheckBox.IsChecked = isTopmost;
            SaveTickerBounds();
            _settingsService.Save(_settings);
        }

        private void TickerWindow_CompactModeChanged(object? sender, bool compactMode)
        {
            _settings.CompactMode = compactMode;
            SaveTickerBounds();
            _settingsService.Save(_settings);
        }

        private async void TickerWindow_SettingsRequested(object? sender, EventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            SaveTickerBounds();
            await StopPollingAsync();
            _tickerWindow?.Hide();
            Show();
            Activate();
            StatusTextBlock.Text = "Stopped";
        }

        private void StocksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StocksListBox.SelectedItem is KisStockSetting stock)
            {
                ApplyStockToEditor(stock);
            }
        }

        private void AddStockButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var stock = BuildStockFromEditor();
                ValidateStock(stock);
                _stocks.Add(stock);
                RefreshStocksList();
                StocksListBox.SelectedItem = stock;
                StatusTextBlock.Text = "Stock added";
            }
            catch (Exception ex)
            {
                AddLog($"Add stock failed: {ex.Message}");
            }
        }

        private void UpdateStockButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StocksListBox.SelectedItem is not KisStockSetting selected)
                {
                    AddLog("Update stock skipped: no stock selected.");
                    return;
                }

                var updated = BuildStockFromEditor();
                ValidateStock(updated);
                var index = _stocks.IndexOf(selected);
                _stocks[index] = updated;
                RefreshStocksList();
                StocksListBox.SelectedIndex = index;
                StatusTextBlock.Text = "Stock updated";
            }
            catch (Exception ex)
            {
                AddLog($"Update stock failed: {ex.Message}");
            }
        }

        private void DeleteStockButton_Click(object sender, RoutedEventArgs e)
        {
            if (StocksListBox.SelectedItem is not KisStockSetting selected)
            {
                AddLog("Delete stock skipped: no stock selected.");
                return;
            }

            var index = StocksListBox.SelectedIndex;
            _stocks.Remove(selected);
            RefreshStocksList();
            StocksListBox.SelectedIndex = Math.Min(index, _stocks.Count - 1);
            StatusTextBlock.Text = "Stock deleted";
        }

        private void ClearStockButton_Click(object sender, RoutedEventArgs e)
        {
            StocksListBox.SelectedItem = null;
            ClearStockEditor();
        }

        private void StockMarketComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingStockEditor)
            {
                return;
            }

            ApplyExchangeDefaultsForMarket(GetComboBoxText(StockMarketComboBox));
        }

        private KisSettings BuildSettingsFromUi()
        {
            var firstStock = _stocks.FirstOrDefault();
            var exchangeCode = firstStock?.ResolveExchangeCode() ?? "BAQ";
            var symbol = firstStock?.Symbol ?? "NVDA";

            return new KisSettings
            {
                AccessTokenUrl = AccessTokenUrlTextBox.Text.Trim(),
                PriceDetailUrl = PriceDetailUrlTextBox.Text.Trim(),
                AppKey = AppKeyTextBox.Text.Trim(),
                AppSecret = AppSecretPasswordBox.Password.Trim(),
                ExchangeCode = exchangeCode,
                Symbol = symbol,
                PollingIntervalSeconds = ParseInt(PollingIntervalTextBox.Text, 2),
                TickerFontSize = ParseDouble(TickerFontSizeTextBox.Text, 13),
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                TickerWindowLeft = _settings.TickerWindowLeft,
                TickerWindowTop = _settings.TickerWindowTop,
                TickerWindowWidth = _settings.TickerWindowWidth,
                TickerWindowHeight = _settings.TickerWindowHeight,
                CompactMode = _settings.CompactMode,
                Stocks = _stocks.Select(CloneStock).ToList()
            };
        }

        private static void ValidateSettings(KisSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.AccessTokenUrl))
            {
                throw new InvalidOperationException("AccessTokenUrl is required.");
            }

            if (string.IsNullOrWhiteSpace(settings.PriceDetailUrl))
            {
                throw new InvalidOperationException("PriceDetailUrl is required.");
            }

            if (string.IsNullOrWhiteSpace(settings.AppKey))
            {
                throw new InvalidOperationException("AppKey is required.");
            }

            if (string.IsNullOrWhiteSpace(settings.AppSecret))
            {
                throw new InvalidOperationException("AppSecret is required.");
            }

            if (settings.PollingIntervalSeconds < 1)
            {
                throw new InvalidOperationException("PollingIntervalSeconds must be 1 or greater.");
            }

            if (settings.TickerFontSize < 9 || settings.TickerFontSize > 32)
            {
                throw new InvalidOperationException("TickerFontSize must be between 9 and 32.");
            }

            if (settings.Stocks.Count == 0)
            {
                throw new InvalidOperationException("At least one stock is required.");
            }

            foreach (var stock in settings.Stocks)
            {
                ValidateStock(stock);
            }
        }

        private static void ValidateStock(KisStockSetting stock)
        {
            if (string.IsNullOrWhiteSpace(stock.Symbol))
            {
                throw new InvalidOperationException("Symbol is required.");
            }

            if (string.IsNullOrWhiteSpace(stock.ResolveExchangeCode()))
            {
                throw new InvalidOperationException("Exchange code is required.");
            }
        }

        private void SaveTickerBounds()
        {
            _tickerWindow?.SaveBoundsTo(_settings);
        }

        private void RefreshStocksList()
        {
            StocksListBox.ItemsSource = null;
            StocksListBox.ItemsSource = _stocks;
        }

        private void ApplyStockToEditor(KisStockSetting stock)
        {
            _isLoadingStockEditor = true;
            try
            {
                StockDisplayNameTextBox.Text = stock.DisplayName;
                StockSymbolTextBox.Text = stock.Symbol;
                SetComboBoxText(StockMarketComboBox, stock.Market);
                SetComboBoxText(ExchangeModeComboBox, stock.ExchangeMode);
                SetComboBoxText(DayExchangeCodeComboBox, stock.DayExchangeCode);
                SetComboBoxText(RegularExchangeCodeComboBox, stock.RegularExchangeCode);
                ManualExchangeCodeTextBox.Text = stock.ManualExchangeCode;
                HoldingQuantityTextBox.Text = FormatDecimal(stock.HoldingQuantity);
                AveragePriceTextBox.Text = FormatDecimal(stock.AveragePrice);
            }
            finally
            {
                _isLoadingStockEditor = false;
            }
        }

        private void ClearStockEditor()
        {
            StockDisplayNameTextBox.Text = string.Empty;
            StockSymbolTextBox.Text = string.Empty;
            SetComboBoxText(StockMarketComboBox, "NASDAQ");
            SetComboBoxText(ExchangeModeComboBox, "Auto");
            ApplyExchangeDefaultsForMarket("NASDAQ");
            HoldingQuantityTextBox.Text = string.Empty;
            AveragePriceTextBox.Text = string.Empty;
        }

        private KisStockSetting BuildStockFromEditor()
        {
            return new KisStockSetting
            {
                DisplayName = StockDisplayNameTextBox.Text.Trim(),
                Symbol = StockSymbolTextBox.Text.Trim().ToUpperInvariant(),
                Market = GetComboBoxText(StockMarketComboBox).Trim(),
                ExchangeMode = GetComboBoxText(ExchangeModeComboBox).Trim(),
                DayExchangeCode = GetComboBoxText(DayExchangeCodeComboBox).Trim().ToUpperInvariant(),
                RegularExchangeCode = GetComboBoxText(RegularExchangeCodeComboBox).Trim().ToUpperInvariant(),
                ManualExchangeCode = ManualExchangeCodeTextBox.Text.Trim().ToUpperInvariant(),
                HoldingQuantity = ParseDecimal(HoldingQuantityTextBox.Text),
                AveragePrice = ParseDecimal(AveragePriceTextBox.Text)
            };
        }

        private void SetBusyUi(bool isBusy)
        {
            StartPollingButton.IsEnabled = !isBusy && _pollingCts is null;
            SaveButton.IsEnabled = !isBusy;
        }

        private void SetPollingUi(bool isPolling)
        {
            StartPollingButton.IsEnabled = !isPolling;
            StopPollingButton.IsEnabled = isPolling;
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            foreach (var line in message.Replace("\r\n", "\n").Split('\n'))
            {
                _logLines.Add($"[{timestamp}] {line}");
            }

            if (_logLines.Count > MaxLogLines)
            {
                _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
            }

            RawLogTextBox.Text = string.Join(Environment.NewLine, _logLines);
            RawLogTextBox.ScrollToEnd();
        }

        private static KisStockSetting CloneStock(KisStockSetting stock)
        {
            return new KisStockSetting
            {
                DisplayName = stock.DisplayName,
                Symbol = stock.Symbol,
                Market = stock.Market,
                ExchangeMode = stock.ExchangeMode,
                DayExchangeCode = stock.DayExchangeCode,
                RegularExchangeCode = stock.RegularExchangeCode,
                ManualExchangeCode = stock.ManualExchangeCode,
                HoldingQuantity = stock.HoldingQuantity,
                AveragePrice = stock.AveragePrice
            };
        }

        private static string GetComboBoxText(ComboBox comboBox)
        {
            if (comboBox.IsEditable && !string.IsNullOrWhiteSpace(comboBox.Text))
            {
                return comboBox.Text;
            }

            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? string.Empty;
            }

            return comboBox.Text;
        }

        private static void SetComboBoxText(ComboBox comboBox, string value)
        {
            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    comboBox.Text = value;
                    return;
                }
            }

            comboBox.SelectedItem = null;
            comboBox.Text = value;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out var parsed) ? Math.Max(1, parsed) : defaultValue;
        }

        private static double ParseDouble(string value, double defaultValue)
        {
            return double.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        private static decimal ParseDecimal(string value)
        {
            return decimal.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;
        }

        private static string FormatDecimal(decimal value)
        {
            return value > 0 ? value.ToString("0.####") : string.Empty;
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(empty)";
            }

            if (value.Length <= 8)
            {
                return new string('*', value.Length);
            }

            return $"{value[..4]}...{value[^4..]}";
        }

        private void ApplyExchangeDefaultsForMarket(string market)
        {
            var normalized = market.Trim().ToUpperInvariant();
            var dayCode = normalized switch
            {
                "NYSE" => "BAY",
                "AMEX" => "BAA",
                _ => "BAQ"
            };
            var regularCode = normalized switch
            {
                "NYSE" => "NYS",
                "AMEX" => "AMS",
                _ => "NAS"
            };

            SetComboBoxText(DayExchangeCodeComboBox, dayCode);
            SetComboBoxText(RegularExchangeCodeComboBox, regularCode);
            ManualExchangeCodeTextBox.Text = dayCode;
        }
    }
}
