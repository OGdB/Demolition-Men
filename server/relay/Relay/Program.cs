using Microsoft.AspNetCore.HttpOverrides;

var app = WebApplication.CreateBuilder(args).Build();
// Read after Build so test overrides and env vars (Relay__MaxRooms=...) are in.
var options = app.Configuration.GetSection("Relay").Get<RelayOptions>() ?? new();
var relay = new RelayServer(options, app.Logger);

// X-Forwarded-For is trusted from loopback (a proxy on the same box) and from the networks
// configured: a Caddy container's Docker network. Without it every player shares the
// proxy's address, and the per-IP limits turn into global ones.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
foreach (var network in options.TrustedProxyNetworks)
    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
app.UseForwardedHeaders(forwarded);
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
    /// CIDRs whose X-Forwarded-For is believed, besides loopback, e.g. "172.18.0.0/16".
    public string[] TrustedProxyNetworks { get; set; } = [];
}
