using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

public sealed class CountersTests : IDisposable
{
    const string Token = "0123456789abcdef-test";
    // As the game's JsonUtility writes them (its CountersTests holds the same two texts): every field, every time.
    const string Day1 = """{"e":"day","p":"web","level":"","stars":0,"players":0,"online":0,"since":1}""";
    const string Level = """{"e":"level_done","p":"android","level":"town-03","stars":2,"players":1,"online":0,"since":0}""";

    readonly string folder = Directory.CreateTempSubdirectory("relay-counters").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    WebApplicationFactory<Program> Start(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Relay:CountersDatabase", Path.Combine(folder, "counters.db"));
            b.UseSetting("Relay:AdminToken", Token);
            foreach (var (key, value) in settings) b.UseSetting("Relay:" + key, value);
        });

    static Task<HttpResponseMessage> Post(HttpClient http, string body) =>
        http.PostAsync("/count", new StringContent(body, Encoding.UTF8, "application/json"));

    static async Task<CountRow[]> Counts(HttpClient http, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/counts" + query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var response = await http.SendAsync(request);
        Assert.Equal(200, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CountRow[]>())!;
    }

    // A request from this address, with a user agent: what a real device's request carries.
    static async Task<int> PostFrom(WebApplicationFactory<Program> app, string address, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var ctx = await app.Server.SendAsync(c =>
        {
            c.Connection.RemoteIpAddress = IPAddress.Parse(address);
            c.Request.Method = "POST";
            c.Request.Path = "/count";
            c.Request.ContentType = "application/json";
            c.Request.Headers.UserAgent = "MarkerAgent/9 (MarkerPhone 12)";
            c.Request.Body = new MemoryStream(bytes);
        });
        return ctx.Response.StatusCode;
    }

    [Fact]
    public async Task Count_AddsOneToACounterPerDayEventAndFields()
    {
        using var app = Start();
        var http = app.CreateClient();
        var before = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.Equal(204, (int)(await Post(http, Level)).StatusCode);
        Assert.Equal(204, (int)(await Post(http, Level)).StatusCode);
        Assert.Equal(204, (int)(await Post(http, Day1)).StatusCode);
        Assert.Equal(204, (int)(await Post(http, """{"e":"first_start","p":"pc"}""")).StatusCode); // unset fields are their defaults
        var after = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var rows = await Counts(http);
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Contains(row.Day, new[] { before, after }));
        Assert.Contains(rows, r => r is { Event: "level_done", Platform: "android", Level: "town-03", Stars: 2, Players: 1, Online: 0, N: 2 });
        Assert.Contains(rows, r => r is { Event: "day", Platform: "web", Level: "", Since: 1, N: 1 });
        Assert.Contains(rows, r => r is { Event: "first_start", Platform: "pc", N: 1 });
    }

    [Theory]
    [InlineData("""{"e":"first_start","p":"web","level":"","stars":0,"players":0,"online":0,"since":0}""")]
    [InlineData("""{"e":"tutorial_done","p":"android","level":"","stars":0,"players":0,"online":0,"since":0}""")]
    [InlineData("""{"e":"share","p":"other","level":"","stars":0,"players":0,"online":0,"since":0}""")]
    [InlineData("""{"e":"invite","p":"android","level":"","stars":0,"players":0,"online":0,"since":0}""")]
    [InlineData("""{"e":"day","p":"pc","level":"","stars":0,"players":0,"online":0,"since":0}""")]
    [InlineData("""{"e":"day","p":"pc","level":"","stars":0,"players":0,"online":0,"since":30}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"tutorial","stars":0,"players":1,"online":0,"since":0}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"daily","stars":3,"players":2,"online":2,"since":0}""")]
    public async Task EveryEventTheGameSends_IsCounted(string body)
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(204, (int)(await Post(http, body)).StatusCode);
        Assert.Equal(1, Assert.Single(await Counts(http)).N);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"e":"level_done"}""")] // no platform
    [InlineData("""{"e":"install","p":"web"}""")] // not on the list
    [InlineData("""{"e":"room","p":"web"}""")] // the relay's own count
    [InlineData("""{"e":"share","p":"iphone 17 pro"}""")]
    [InlineData("""{"e":"share","p":"web","id":"4f1c"}""")] // a field we don't take
    [InlineData("""{"E":"share","p":"web"}""")]
    [InlineData("""{"e":"share","p":"web","level":"town-01"}""")] // another event's field
    [InlineData("""{"e":"invite","p":"web","level":"ABCDEF"}""")] // a room code in a field: an invite has none
    [InlineData("""{"e":"invite","p":"web","code":"ABCDEF"}""")]
    [InlineData("""{"e":"invite","p":"web","players":2}""")]
    [InlineData("""{"e":"first_start","p":"web","since":1}""")]
    [InlineData("""{"e":"day","p":"web","since":2}""")]
    [InlineData("""{"e":"day","p":"web","since":-1}""")]
    [InlineData("""{"e":"day","p":"web","since":"1"}""")]
    [InlineData("""{"e":"day","p":"web","since":1,"stars":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"","stars":1,"players":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"Hello World","stars":1,"players":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01\n","stars":1,"players":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"aaaaaaaaaaaaaaaaaaaaaaaaa","stars":1,"players":1}""")] // 25
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":4,"players":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":1.5,"players":1}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":1,"players":0}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":1,"players":3}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":1,"players":2,"online":3}""")]
    [InlineData("""{"e":"level_done","p":"web","level":"town-01","stars":1,"players":1,"since":7}""")]
    public async Task Junk_IsRefusedAndNothingIsCounted(string body)
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(400, (int)(await Post(http, body)).StatusCode);
        Assert.Empty(await Counts(http));
    }

    [Fact]
    public async Task BodyOverTheCap_Is413()
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(413, (int)(await Post(http, Level + new string(' ', 300))).StatusCode);
        Assert.Empty(await Counts(http));
    }

    [Fact]
    public async Task OneAddress_IsRateLimited_AnotherIsNot()
    {
        using var app = Start(("CountsPerMinute", "2"));
        Assert.Equal(204, await PostFrom(app, "203.0.113.77", Level));
        Assert.Equal(400, await PostFrom(app, "203.0.113.77", "junk")); // junk uses the allowance too
        Assert.Equal(429, await PostFrom(app, "203.0.113.77", Level));
        Assert.Equal(204, await PostFrom(app, "203.0.113.78", Level));
        Assert.Equal(2, Assert.Single(await Counts(app.CreateClient())).N);
    }

    [Fact]
    public async Task ADay_HoldsOnlySoManyCounters()
    {
        using var app = Start(("CountRowsPerDay", "2"));
        var http = app.CreateClient();
        foreach (var level in new[] { "a", "b", "c", "a" })
            Assert.Equal(204, (int)(await Post(http, Level.Replace("town-03", level))).StatusCode);
        var rows = await Counts(http);
        Assert.Equal(new[] { "a", "b" }, rows.Select(r => r.Level).ToArray());
        Assert.Equal(new[] { 2L, 1L }, rows.Select(r => r.N).ToArray()); // counters that exist still go up
    }

    [Fact]
    public async Task NothingButCountersIsStored()
    {
        using (var app = Start())
        {
            Assert.Equal(204, await PostFrom(app, "203.0.113.77", Level));
            Assert.Equal(204, await PostFrom(app, "203.0.113.77", Day1));
            Assert.Equal(400, await PostFrom(app, "203.0.113.77", """{"e":"share","p":"web","name":"MarkerName"}"""));
        } // closed: everything is on disk

        // Every file the relay wrote: no address, no user agent, nothing of a refused event.
        foreach (var file in Directory.GetFiles(folder))
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain("203.0.113", text);
            Assert.DoesNotContain("Marker", text);
        }

        // One table, and its columns are the day, the event, its fields and the count.
        using var db = new SqliteConnection("Data Source=" + Path.Combine(folder, "counters.db"));
        db.Open();
        Assert.Equal(new[] { "counts" }, Column(db, "SELECT name FROM sqlite_master WHERE type IN ('table', 'view')").ToArray());
        Assert.Equal(new[] { "day", "event", "platform", "level", "stars", "players", "online", "since", "n" },
            Column(db, "SELECT name FROM pragma_table_info('counts')").ToArray());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", Assert.Single(Column(db, "SELECT DISTINCT day FROM counts"))); // the day, no time
    }

    static List<string> Column(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var values = new List<string>();
        while (r.Read()) values.Add(r.GetString(0));
        return values;
    }

    [Fact]
    public async Task AdminCounts_NeedTheToken_AndTakeDays()
    {
        using var app = Start();
        var http = app.CreateClient();
        await Post(http, Level);
        Assert.Equal(404, (int)(await http.GetAsync("/admin/counts")).StatusCode);
        var wrong = new HttpRequestMessage(HttpMethod.Get, "/admin/counts");
        wrong.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token + "x");
        Assert.Equal(404, (int)(await http.SendAsync(wrong)).StatusCode);

        Assert.Single(await Counts(http, "?from=2000-01-01&to=9999-12-31"));
        Assert.Empty(await Counts(http, "?to=2000-01-01"));
        var bad = new HttpRequestMessage(HttpMethod.Get, "/admin/counts?from=yesterday");
        bad.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        Assert.Equal(400, (int)(await http.SendAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task NoTokenConfigured_NoAdmin()
    {
        using var app = Start(("AdminToken", ""));
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/counts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");
        Assert.Equal(404, (int)(await app.CreateClient().SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("")] // not configured
    [InlineData("/proc/nowhere/counters.db")] // can't be opened
    public async Task WithoutADatabase_NoCounters_AndTheRelayRuns(string database)
    {
        using var app = Start(("CountersDatabase", database));
        var http = app.CreateClient();
        Assert.Equal("ok", await http.GetStringAsync("/health"));
        Assert.Equal(404, (int)(await Post(http, Level)).StatusCode);
        var host = await app.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws?v=1&role=host"), CancellationToken.None);
        Assert.Equal(System.Net.WebSockets.WebSocketState.Open, host.State);
    }

    [Fact]
    public async Task BrowserBuild_MayPostFromAnotherSite()
    {
        using var app = Start();
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/count");
        preflight.Headers.Add("Origin", "https://html-classic.itch.zone");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-unity-version");
        var http = app.CreateClient();
        var response = await http.SendAsync(preflight);
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("content-type,x-unity-version", response.Headers.GetValues("Access-Control-Allow-Headers").Single());

        // And the post itself, as a browser sends it.
        var post = new HttpRequestMessage(HttpMethod.Post, "/count") { Content = new StringContent(Level, Encoding.UTF8, "application/json") };
        post.Headers.Add("Origin", "https://html-classic.itch.zone");
        response = await http.SendAsync(post);
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task AllowedOrigins_AlsoHoldForTheBrowserBuildsCounts()
    {
        using var app = Start(("AllowedOrigins:0", "https://html-classic.itch.zone"));
        foreach (var (origin, allowed) in new[] { ("https://html-classic.itch.zone", true), ("https://elsewhere.example", false) })
        {
            var preflight = new HttpRequestMessage(HttpMethod.Options, "/count");
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            var response = await app.CreateClient().SendAsync(preflight);
            Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task Relay_CountsRoomsAndJoins_ByBrowserOrApp()
    {
        using var app = Start();
        var buffer = new byte[64];
        var host = await app.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws?v=1&role=host"), CancellationToken.None);
        var code = Encoding.ASCII.GetString(buffer, 0, (await host.ReceiveAsync(buffer, CancellationToken.None)).Count)["code:".Length..];
        var browser = app.Server.CreateWebSocketClient();
        browser.ConfigureRequest = request => request.Headers.Origin = "https://html-classic.itch.zone";
        var joiner = await browser.ConnectAsync(new Uri("ws://localhost/ws?v=1&role=join&code=" + code), CancellationToken.None);
        await joiner.ReceiveAsync(buffer, CancellationToken.None); // "joined": the join is counted by now
        var miss = await app.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws?v=1&role=join&code=ZZZZZZ"), CancellationToken.None);
        await miss.ReceiveAsync(buffer, CancellationToken.None); // closed 4001: not a join

        var rows = await Counts(app.CreateClient());
        Assert.Equal(2, rows.Length);
        Assert.Contains(rows, r => r is { Event: "room", Platform: "app", N: 1 });
        Assert.Contains(rows, r => r is { Event: "join", Platform: "web", N: 1 });
        foreach (var file in Directory.GetFiles(folder)) // never the room code
            Assert.DoesNotContain(code, File.ReadAllText(file, Encoding.Latin1));
    }
}
