using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
// No Server header: responses don't say what the relay runs on.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
var app = builder.Build();
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

// The game's privacy policy (store listing, AdMob consent form, the game's credits). Read once;
// like every request here it isn't logged. The page loads nothing from anywhere.
var privacyPage = File.ReadAllText(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "privacy.html"));
app.MapGet("/privacy", (HttpContext ctx) =>
{
    ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    return Results.Content(privacyPage, "text/html; charset=utf-8");
});

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
