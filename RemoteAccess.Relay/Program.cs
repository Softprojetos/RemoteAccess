using RemoteAccess.Relay;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RelayHub>();

var app = builder.Build();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async (HttpContext ctx, RelayHub hub) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("WebSocket connections only");
        return;
    }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await hub.HandleConnectionAsync(ws, ctx.RequestAborted);
});

app.MapGet("/", () => Results.Json(new
{
    service = "RemoteAccess Relay",
    status = "running",
    connections = 0
}));

app.MapGet("/health", () => Results.Ok("ok"));

Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║     RemoteAccess Relay Server        ║");
Console.WriteLine("║     ws://localhost:5050/ws            ║");
Console.WriteLine("╚══════════════════════════════════════╝");

app.Run("http://0.0.0.0:5050");
