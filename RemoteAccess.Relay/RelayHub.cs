using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using RemoteAccess.Shared;

namespace RemoteAccess.Relay;

public class RelayHub
{
    private readonly ConcurrentDictionary<string, HostSession> _hosts = new();
    private readonly ILogger<RelayHub> _logger;

    public RelayHub(ILogger<RelayHub> logger) => _logger = logger;

    public int ConnectionCount => _hosts.Count;

    public async Task HandleConnectionAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        string? hostId = null;
        HostSession? session = null;
        bool isViewer = false;

        try
        {
            // First message must be register or connect
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var msg = Protocol.Deserialize(json);

            switch (msg)
            {
                case RegisterMessage reg:
                    hostId = reg.Id;
                    session = new HostSession(ws, reg.Password, reg.Hostname);
                    _hosts[hostId] = session;
                    _logger.LogInformation("Host registered: {Id} ({Host})", hostId, reg.Hostname);
                    await RunHostLoop(session, hostId, ct);
                    break;

                case ConnectMessage conn:
                    isViewer = true;
                    hostId = conn.TargetId;
                    if (!_hosts.TryGetValue(conn.TargetId, out session))
                    {
                        await SendText(ws, new ErrorMessage { Message = "ID não encontrado" }, ct);
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "ID not found", ct);
                        return;
                    }
                    if (session.Password != conn.Password)
                    {
                        await SendText(ws, new ErrorMessage { Message = "Senha incorreta" }, ct);
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Wrong password", ct);
                        return;
                    }
                    session.Viewer = ws;
                    _logger.LogInformation("Viewer connected to host: {Id}", conn.TargetId);

                    // Notify host
                    await SendText(session.Host, new ViewerConnectedMessage(), ct);
                    await RunViewerLoop(session, ws, ct);
                    break;

                default:
                    await SendText(ws, new ErrorMessage { Message = "Expected register or connect" }, ct);
                    return;
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning("WebSocket error: {Msg}", ex.Message);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (isViewer && session != null)
            {
                session.Viewer = null;
                // Notify host that viewer left
                try
                {
                    if (session.Host.State == WebSocketState.Open)
                        await SendText(session.Host, new ViewerDisconnectedMessage(), CancellationToken.None);
                }
                catch { }
                _logger.LogInformation("Viewer disconnected from: {Id}", hostId);
            }
            else if (!isViewer && hostId != null)
            {
                _hosts.TryRemove(hostId, out _);
                // Notify viewer if any
                if (session?.Viewer is { State: WebSocketState.Open } viewer)
                {
                    try
                    {
                        await SendText(viewer, new HostDisconnectedMessage(), CancellationToken.None);
                    }
                    catch { }
                }
                _logger.LogInformation("Host unregistered: {Id}", hostId);
            }
        }
    }

    private async Task RunHostLoop(HostSession session, string hostId, CancellationToken ct)
    {
        var buffer = new byte[1024 * 512]; // 512KB for screen frames
        while (session.Host.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await session.Host.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketCloseStatus.NormalClosure ||
                result.MessageType == WebSocketMessageType.Close)
                break;

            // Forward to viewer if connected
            if (session.Viewer is { State: WebSocketState.Open } viewer)
            {
                try
                {
                    await viewer.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        ct);
                }
                catch
                {
                    session.Viewer = null;
                }
            }
        }
    }

    private async Task RunViewerLoop(HostSession session, WebSocket viewer, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (viewer.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await viewer.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketCloseStatus.NormalClosure ||
                result.MessageType == WebSocketMessageType.Close)
                break;

            // Forward to host
            if (session.Host.State == WebSocketState.Open)
            {
                try
                {
                    await session.Host.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        ct);
                }
                catch { break; }
            }
        }
    }

    private static async Task SendText(WebSocket ws, ProtocolMessage msg, CancellationToken ct)
    {
        var bytes = Protocol.SerializeToBytes(msg);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }
}

public class HostSession
{
    public WebSocket Host { get; }
    public string Password { get; }
    public string Hostname { get; }
    public WebSocket? Viewer { get; set; }

    public HostSession(WebSocket host, string password, string hostname)
    {
        Host = host;
        Password = password;
        Hostname = hostname;
    }
}
