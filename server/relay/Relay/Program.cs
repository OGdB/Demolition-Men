using Microsoft.AspNetCore.HttpOverrides;

var app = WebApplication.CreateBuilder(args).Build();
// Read after Build so test overrides and env vars (Relay__MaxRooms=...) are in.
var options = app.Configuration.GetSection("Relay").Get<RelayOptions>() ?? new();
var relay = new RelayServer(options, app.Logger);

// The defaults trust X-Forwarded-For only from loopback: Caddy on the same box.
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor });
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = options.KeepAliveInterval,
    KeepAliveTimeout = options.KeepAliveTimeout,
});
app.MapGet("/health", () => "ok");
app.Map("/ws", relay.Handle);

_ = relay.Tick(app.Lifetime.ApplicationStopping);
app.Run();

public partial class Program;

public sealed class RelayOptions
{
    public int Protocol { get; set; } = 1;
    public int MaxRooms { get; set; } = 500;
    public int MaxRoomsPerIp { get; set; } = 3;
    public int MaxMessageBytes { get; set; } = 65536;
    public int ConnectsPerMinute { get; set; } = 30;
    public int FailedJoinsPerMinute { get; set; } = 10;
    public TimeSpan LobbyTimeout { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan MaxRoomLifetime { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public string[] AllowedOrigins { get; set; } = [];
}
