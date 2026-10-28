using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Demolition.Ranked;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The ranked boards' server (docs/ranked.md §8). Its own service next to the relay (K-D7).
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 512 * 1024; // a log is a few KB, sealed and in base64
});
// The game's messages are classes with public fields, named as they're sent (RankedApi).
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.IncludeFields = true;
    json.SerializerOptions.PropertyNamingPolicy = null;
});
// Browsers (the WebGL build on another site) may call it: nothing here rides on cookies.
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().WithMethods("GET", "POST")));
builder.Services.AddSingleton(TimeProvider.System);
var app = builder.Build();

var options = app.Configuration.GetSection("Ranked").Get<RankedOptions>() ?? new RankedOptions();
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
foreach (string network in options.TrustedProxyNetworks)
    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
app.UseForwardedHeaders(forwarded);
app.UseCors();

var store = new Store(options.Database);
app.Lifetime.ApplicationStopped.Register(store.Dispose);
string content = string.IsNullOrEmpty(options.Content) ? Path.Combine(AppContext.BaseDirectory, "Content") : options.Content;
var service = new RankedService(options, new Levels(content), store,
    app.Services.GetRequiredService<TimeProvider>(), app.Logger);

static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "?";

app.MapGet("/health", () => "ok");
app.MapPost("/start", (StartRequest request, HttpContext ctx) => service.Start(request, Ip(ctx)));
app.MapPost("/submit", (SubmitRequest request) => service.Submit(request));
app.MapGet("/board", (int? day, string mode, string player) => service.Board(day ?? 0, mode, player));
app.MapGet("/ghost", (int? day, string mode) => service.Ghost(day ?? 0, mode));
app.MapPost("/report", (ReportRequest request, HttpContext ctx) => service.Report(request, Ip(ctx)));

// Review, for the bearer of Ranked__AdminToken (docs/ranked.md §6): curl -H "Authorization: Bearer ..."
RouteGroupBuilder admin = app.MapGroup("/admin").AddEndpointFilter(async (context, next) =>
{
    string expected = options.AdminToken ?? "";
    string given = context.HttpContext.Request.Headers.Authorization.ToString();
    byte[] a = Encoding.UTF8.GetBytes("Bearer " + expected), b = Encoding.UTF8.GetBytes(given);
    if (expected.Length < 16 || !CryptographicOperations.FixedTimeEquals(a, b))
        return Results.NotFound(); // no token configured, or the wrong one: there is nothing here
    return await next(context);
});
admin.MapGet("/runs", (int? limit) => service.Review(limit ?? 100));
admin.MapPost("/runs/{run:long}/approve", (long run) => service.Approve(run));
admin.MapPost("/players/{player}/hide", (string player) => service.Hide(player, true));
admin.MapPost("/players/{player}/show", (string player) => service.Hide(player, false));

// Old runs forgotten (RetentionDays), then a copy of the database a day, the newest few kept (BackupFolder, BackupsKept).
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
    do
    {
        try
        {
            service.Purge();
            service.Backup();
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "Backup failed");
        }
    } while (await timer.WaitForNextTickAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false));
});

app.Run();

public partial class Program;
