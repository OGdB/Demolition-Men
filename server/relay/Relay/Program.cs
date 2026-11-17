using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
// No Server header: responses don't say what the relay runs on.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.AddCors();
var app = builder.Build();
// Read after Build so test overrides and env vars (Relay__MaxRooms=...) are in.
var options = app.Configuration.GetSection("Relay").Get<RelayOptions>() ?? new();

// The game's anonymous counters (Counters.cs). Off with no database, and when it doesn't open:
// co-op must not go down with them.
Counters? counters = null;
if (options.CountersDatabase.Length > 0)
    try
    {
        counters = new Counters(options);
        app.Lifetime.ApplicationStopped.Register(counters.Dispose);
    }
    catch (Exception e) { app.Logger.LogError(e, "Counters are off: their database didn't open"); }
var relay = new RelayServer(options, app.Logger, counters);

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
app.UseCors(); // only /count asks for it
app.MapGet("/health", () => "ok");
app.Map("/ws", relay.Handle);

if (counters != null)
{
    // The browser build posts from another site: nothing here rides on cookies. Any header, as the
    // ranked server allows: Unity may add its own (X-Unity-Version) to the request.
    app.MapPost("/count", counters.Post).RequireCors(cors =>
        (options.AllowedOrigins.Length > 0 ? cors.WithOrigins(options.AllowedOrigins) : cors.AllowAnyOrigin())
            .AllowAnyHeader().WithMethods("POST"));
    // The owner's read, as the ranked server's /admin: curl -H "Authorization: Bearer ..."
    app.MapGet("/admin/counts", (string? from, string? to) =>
    {
        from ??= DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        to ??= "9999-12-31";
        return Day(from) && Day(to) ? Results.Ok(counters.Read(from, to)) : Results.BadRequest();
    }).AddEndpointFilter(async (context, next) =>
    {
        byte[] a = Encoding.UTF8.GetBytes("Bearer " + options.AdminToken);
        byte[] b = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers.Authorization.ToString());
        if (options.AdminToken.Length < 16 || !CryptographicOperations.FixedTimeEquals(a, b))
            return Results.NotFound(); // no token configured, or the wrong one: there is nothing here
        return await next(context);
    });
}

static bool Day(string text) => DateOnly.TryParseExact(text, "yyyy-MM-dd", out _);

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
    /// The counters' SQLite file; empty: no /count, and rooms and joins aren't counted.
    public string CountersDatabase { get; set; } = "";
    /// Counts one address may post in a minute.
    public int CountsPerMinute { get; set; } = 60;
    /// Different counters one day may hold; past it only counters that exist go up.
    public int CountRowsPerDay { get; set; } = 2000;
    /// Reads /admin/counts as "Authorization: Bearer ..."; under 16 characters: no /admin.
    public string AdminToken { get; set; } = "";
}
