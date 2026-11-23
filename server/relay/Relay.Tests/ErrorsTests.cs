using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

public sealed class ErrorsTests : IDisposable
{
    const string Token = "0123456789abcdef-test";
    // As the game's JsonUtility writes one (its ErrorReportsTests holds the same text).
    const string Null = """{"v":"1.0.3","p":"android","device":"samsung SM-A515F","msg":"NullReferenceException: Object reference not set to an instance of an object","stack":"Demolition.Run.RunHud.LateUpdate ()\n"}""";

    readonly string folder = Directory.CreateTempSubdirectory("relay-errors").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    WebApplicationFactory<Program> Start(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Relay:ErrorsDatabase", Path.Combine(folder, "errors.db"));
            b.UseSetting("Relay:AdminToken", Token);
            foreach (var (key, value) in settings) b.UseSetting("Relay:" + key, value);
        });

    static Task<HttpResponseMessage> Post(HttpClient http, string body) =>
        http.PostAsync("/error", new StringContent(body, Encoding.UTF8, "application/json"));

    static string Report(string v = "1.0.3", string p = "android", string device = "samsung SM-A515F",
        string msg = "NullReferenceException: Object reference not set to an instance of an object", string stack = "A.B () (at <x>:0)\n") =>
        JsonSerializer.Serialize(new { v, p, device, msg, stack });

    static async Task<ErrorRow[]> Rows(HttpClient http, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/errors" + query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var response = await http.SendAsync(request);
        Assert.Equal(200, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ErrorRow[]>())!;
    }

    [Fact]
    public async Task OneError_IsOneRow_CountedAndKeepingTheLatestSample()
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(204, (int)(await Post(http, Null)).StatusCode);
        // The same type and stack with another message and device: the same bug.
        Assert.Equal(204, (int)(await Post(http, Report(msg: "NullReferenceException: other words", device: "Pixel 8",
            stack: "Demolition.Run.RunHud.LateUpdate ()\n"))).StatusCode);

        var row = Assert.Single(await Rows(http));
        Assert.Equal(("1.0.3", "android", 2L), (row.Version, row.Platform, row.N));
        Assert.Equal(("NullReferenceException: other words", "Pixel 8"), (row.Message, row.Device));
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), row.LastDay);
    }

    [Fact]
    public async Task AnotherStackVersionOrPlatform_IsAnotherRow_MostFrequentFirst()
    {
        using var app = Start();
        var http = app.CreateClient();
        foreach (var body in new[] { Report(), Report(), Report(stack: "C.D ()\n"), Report(v: "1.0.4"), Report(p: "web") })
            Assert.Equal(204, (int)(await Post(http, body)).StatusCode);
        var rows = await Rows(http);
        Assert.Equal(4, rows.Length);
        Assert.Equal(2, rows[0].N);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"v":"1.0.3","p":"android","device":"","msg":"X: y","stack":"","id":"MarkerId"}""")] // a field we don't take
    [InlineData("""{"v":"1.0 3","p":"android","device":"","msg":"X: y","stack":""}""")] // not a version
    [InlineData("""{"v":"1.0.3","p":"ios","device":"","msg":"X: y","stack":""}""")] // not a platform we have
    [InlineData("""{"v":"1.0.3","p":"web","device":"","msg":"","stack":""}""")] // no message
    [InlineData("""{"v":"1.0.3","p":"web","device":"a\nb","msg":"X: y","stack":""}""")] // a device on two lines
    [InlineData("""{"v":"1.0.3","p":"web","device":"","msg":"X\u0000y","stack":""}""")] // control characters
    [InlineData("""{"p":"web","device":"","msg":"X: y","stack":""}""")] // no version
    public async Task Junk_IsRefusedAndNothingIsStored(string body)
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(400, (int)(await Post(http, body)).StatusCode);
        Assert.Empty(await Rows(http));
    }

    [Fact]
    public async Task OverTheCaps_IsRefused()
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(400, (int)(await Post(http, Report(msg: new string('x', Errors.MaxMessage + 1)))).StatusCode);
        Assert.Equal(400, (int)(await Post(http, Report(stack: new string('x', Errors.MaxStack + 1)))).StatusCode);
        Assert.Equal(413, (int)(await Post(http, Report(stack: new string('"', 5000)))).StatusCode); // escaped: over the body cap
        Assert.Equal(204, (int)(await Post(http, Report(msg: new string('x', Errors.MaxMessage), stack: new string('y', Errors.MaxStack)))).StatusCode);
    }

    [Fact]
    public async Task OneAddress_IsRateLimited()
    {
        using var app = Start(("ErrorsPerMinute", "2"));
        var http = app.CreateClient();
        Assert.Equal(204, (int)(await Post(http, Null)).StatusCode);
        Assert.Equal(204, (int)(await Post(http, Null)).StatusCode);
        Assert.Equal(429, (int)(await Post(http, Null)).StatusCode);
        Assert.Equal(2, Assert.Single(await Rows(http)).N); // the refused one isn't counted
    }

    [Fact]
    public async Task TheFile_HoldsOnlySoManyErrors()
    {
        using var app = Start(("ErrorRows", "2"));
        var http = app.CreateClient();
        foreach (var stack in new[] { "a\n", "b\n", "c\n", "a\n" })
            Assert.Equal(204, (int)(await Post(http, Report(stack: stack))).StatusCode);
        var rows = await Rows(http);
        Assert.Equal(new[] { "a\n", "b\n" }, rows.Select(r => r.Stack).Order().ToArray());
        Assert.Equal(2, rows.Single(r => r.Stack == "a\n").N); // errors it has still go up
    }

    [Fact]
    public async Task NothingButTheReportIsStored()
    {
        using (var app = Start())
        {
            var bytes = Encoding.UTF8.GetBytes(Null);
            var ctx = await app.Server.SendAsync(c =>
            {
                c.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.77");
                c.Request.Method = "POST";
                c.Request.Path = "/error";
                c.Request.ContentType = "application/json";
                c.Request.Headers.UserAgent = "MarkerAgent/9 (MarkerPhone 12)";
                c.Request.Body = new MemoryStream(bytes);
            });
            Assert.Equal(204, ctx.Response.StatusCode);
        }

        foreach (var file in Directory.GetFiles(folder))
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain("203.0.113", text);
            Assert.DoesNotContain("Marker", text);
        }
        using var db = new SqliteConnection("Data Source=" + Path.Combine(folder, "errors.db"));
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('errors')";
        using var r = cmd.ExecuteReader();
        var columns = new List<string>();
        while (r.Read()) columns.Add(r.GetString(0));
        Assert.Equal(new[] { "sig", "version", "platform", "message", "stack", "device", "first_day", "last_day", "n" }, columns);
    }

    [Fact]
    public async Task AdminErrors_NeedTheToken()
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal(404, (int)(await http.GetAsync("/admin/errors")).StatusCode);
        Assert.Empty(await Rows(http, "?from=2026-01-01"));
        var bad = new HttpRequestMessage(HttpMethod.Get, "/admin/errors?from=yesterday");
        bad.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        Assert.Equal(400, (int)(await http.SendAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task WithoutADatabase_NoErrors_AndTheRelayRuns()
    {
        using var app = Start(("ErrorsDatabase", ""));
        var http = app.CreateClient();
        Assert.Equal(404, (int)(await Post(http, Null)).StatusCode);
        Assert.Equal("ok", await http.GetStringAsync("/health"));
    }

    [Fact]
    public async Task BrowserBuild_MayPostFromAnotherSite()
    {
        using var app = Start();
        var http = app.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/error");
        request.Headers.Add("Origin", "https://html-classic.itch.zone");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        var response = await http.SendAsync(request);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
