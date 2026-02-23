using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteAccess.Shared;

// ── Message envelope ──────────────────────────────────────────────
[JsonDerivedType(typeof(RegisterMessage), "register")]
[JsonDerivedType(typeof(ConnectMessage), "connect")]
[JsonDerivedType(typeof(ViewerConnectedMessage), "viewer_connected")]
[JsonDerivedType(typeof(ViewerDisconnectedMessage), "viewer_disconnected")]
[JsonDerivedType(typeof(HostDisconnectedMessage), "host_disconnected")]
[JsonDerivedType(typeof(ScreenInfoMessage), "screen_info")]
[JsonDerivedType(typeof(InputMessage), "input")]
[JsonDerivedType(typeof(ClipboardMessage), "clipboard")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(PingMessage), "ping")]
[JsonDerivedType(typeof(PongMessage), "pong")]
public abstract class ProtocolMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

// ── Registration / Connection ─────────────────────────────────────
public class RegisterMessage : ProtocolMessage
{
    public override string Type => "register";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
    [JsonPropertyName("hostname")] public string Hostname { get; set; } = "";
}

public class ConnectMessage : ProtocolMessage
{
    public override string Type => "connect";
    [JsonPropertyName("targetId")] public string TargetId { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public class ViewerConnectedMessage : ProtocolMessage
{
    public override string Type => "viewer_connected";
    [JsonPropertyName("viewerName")] public string ViewerName { get; set; } = "";
}

public class ViewerDisconnectedMessage : ProtocolMessage
{
    public override string Type => "viewer_disconnected";
}

public class HostDisconnectedMessage : ProtocolMessage
{
    public override string Type => "host_disconnected";
}

// ── Screen info ───────────────────────────────────────────────────
public class ScreenInfoMessage : ProtocolMessage
{
    public override string Type => "screen_info";
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

// ── Input commands ────────────────────────────────────────────────
public class InputMessage : ProtocolMessage
{
    public override string Type => "input";
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("button")] public string Button { get; set; } = "left";
    [JsonPropertyName("keyCode")] public int KeyCode { get; set; }
    [JsonPropertyName("delta")] public int Delta { get; set; }
}

// ── Clipboard ─────────────────────────────────────────────────────
public class ClipboardMessage : ProtocolMessage
{
    public override string Type => "clipboard";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}

// ── Error ─────────────────────────────────────────────────────────
public class ErrorMessage : ProtocolMessage
{
    public override string Type => "error";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// ── Keep-alive ────────────────────────────────────────────────────
public class PingMessage : ProtocolMessage
{
    public override string Type => "ping";
}

public class PongMessage : ProtocolMessage
{
    public override string Type => "pong";
}

// ── Serialization helper ──────────────────────────────────────────
public static class Protocol
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ProtocolMessage msg) =>
        JsonSerializer.Serialize(msg, msg.GetType(), Options);

    public static ProtocolMessage? Deserialize(string json) =>
        JsonSerializer.Deserialize<ProtocolMessage>(json, Options);

    public static byte[] SerializeToBytes(ProtocolMessage msg) =>
        JsonSerializer.SerializeToUtf8Bytes(msg, msg.GetType(), Options);
}
