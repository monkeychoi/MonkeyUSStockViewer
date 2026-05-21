using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisRealtimeWebSocketClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _webSocket;

    public event Action<string>? RawMessageReceived;

    public event Action<string>? LogReceived;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        await DisposeSocketAsync();

        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        LogReceived?.Invoke($"Connecting WebSocket: {uri}");
        await _webSocket.ConnectAsync(uri, cancellationToken);
        LogReceived?.Invoke($"WebSocket state: {_webSocket.State}");
    }

    public Task SubscribeAsync(KisSettings settings, string approvalKey, CancellationToken cancellationToken)
    {
        return SendSubscriptionAsync(settings, approvalKey, "1", cancellationToken);
    }

    public Task UnsubscribeAsync(KisSettings settings, string approvalKey, CancellationToken cancellationToken)
    {
        return SendSubscriptionAsync(settings, approvalKey, "2", cancellationToken);
    }

    public async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var socket = _webSocket ?? throw new InvalidOperationException("WebSocket is not connected.");
        var buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var messageBuffer = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        LogReceived?.Invoke($"WebSocket close received: {socket.CloseStatus} {socket.CloseStatusDescription}");
                        return;
                    }

                    messageBuffer.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());

                if (IsPingPong(message))
                {
                    await SendTextAsync(message, cancellationToken);
                    LogReceived?.Invoke("PINGPONG response sent.");
                }

                RawMessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            LogReceived?.Invoke("Receive loop canceled.");
        }
        catch (ObjectDisposedException)
        {
            LogReceived?.Invoke("Receive loop stopped after socket disposal.");
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"Receive loop error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var socket = _webSocket;
        if (socket is null)
        {
            return;
        }

        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            LogReceived?.Invoke($"WebSocket close error: {ex.Message}");
        }
        finally
        {
            await DisposeSocketAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSocketAsync();
        _sendLock.Dispose();
    }

    private async Task SendSubscriptionAsync(
        KisSettings settings,
        string approvalKey,
        string trType,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            header = new Dictionary<string, string>
            {
                ["approval_key"] = approvalKey,
                ["custtype"] = "P",
                ["tr_type"] = trType,
                ["content-type"] = "utf-8"
            },
            body = new
            {
                input = new
                {
                    tr_id = settings.TrId,
                    tr_key = settings.TrKey
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        await SendTextAsync(json, cancellationToken);

        var action = trType == "1" ? "Subscribe" : "Unsubscribe";
        LogReceived?.Invoke($"{action} sent: tr_id={settings.TrId}, tr_key={settings.TrKey}");
    }

    private async Task SendTextAsync(string message, CancellationToken cancellationToken)
    {
        var socket = _webSocket ?? throw new InvalidOperationException("WebSocket is not connected.");
        if (socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException($"WebSocket is not open. State={socket.State}");
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task DisposeSocketAsync()
    {
        if (_webSocket is not null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }

        await Task.CompletedTask;
    }

    private static bool IsPingPong(string message)
    {
        if (!message.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (!root.TryGetProperty("header", out var header))
            {
                return false;
            }

            return header.TryGetProperty("tr_id", out var trId)
                && string.Equals(trId.GetString(), "PINGPONG", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
