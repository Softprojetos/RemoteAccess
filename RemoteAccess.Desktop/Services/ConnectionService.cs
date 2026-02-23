using System.Net.WebSockets;
using System.Text;
using RemoteAccess.Shared;

namespace RemoteAccess.Desktop.Services;

public class ConnectionService : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly string _relayUrl;

    public event Action<ProtocolMessage>? OnTextMessage;
    public event Action<byte[]>? OnBinaryMessage;
    public event Action<string>? OnDisconnected;
    public event Action? OnConnected;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public ConnectionService(string relayUrl)
    {
        _relayUrl = relayUrl.TrimEnd('/') + "/ws";
    }

    public async Task<bool> ConnectAsHostAsync(string id, string password)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            await _ws.ConnectAsync(new Uri(_relayUrl), _cts.Token);

            var reg = new RegisterMessage
            {
                Id = id,
                Password = password,
                Hostname = Environment.MachineName
            };
            await SendTextAsync(reg);
            OnConnected?.Invoke();

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            OnDisconnected?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task<bool> ConnectAsViewerAsync(string targetId, string password)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            await _ws.ConnectAsync(new Uri(_relayUrl), _cts.Token);

            var conn = new ConnectMessage
            {
                TargetId = targetId,
                Password = password
            };
            await SendTextAsync(conn);
            OnConnected?.Invoke();

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            OnDisconnected?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task SendTextAsync(ProtocolMessage msg)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var bytes = Protocol.SerializeToBytes(msg);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    public async Task SendBinaryAsync(byte[] data)
    {
        if (_ws?.State != WebSocketState.Open) return;
        await _ws.SendAsync(data, WebSocketMessageType.Binary, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1024 * 1024]; // 1MB buffer for frames
        try
        {
            while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (!result.EndOfMessage)
                {
                    // Handle large messages by assembling them
                    using var ms = new MemoryStream();
                    ms.Write(buffer, 0, result.Count);
                    while (!result.EndOfMessage)
                    {
                        result = await _ws.ReceiveAsync(buffer, ct);
                        ms.Write(buffer, 0, result.Count);
                    }
                    var fullData = ms.ToArray();

                    if (result.MessageType == WebSocketMessageType.Binary)
                        OnBinaryMessage?.Invoke(fullData);
                    else
                    {
                        var json = Encoding.UTF8.GetString(fullData);
                        var msg = Protocol.Deserialize(json);
                        if (msg != null) OnTextMessage?.Invoke(msg);
                    }
                }
                else
                {
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var data = new byte[result.Count];
                        Buffer.BlockCopy(buffer, 0, data, 0, result.Count);
                        OnBinaryMessage?.Invoke(data);
                    }
                    else
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var msg = Protocol.Deserialize(json);
                        if (msg != null) OnTextMessage?.Invoke(msg);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            OnDisconnected?.Invoke("Conexão encerrada");
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _ws?.Dispose();
    }
}
