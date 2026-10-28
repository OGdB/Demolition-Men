using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Demolition.Levels;
using Demolition.Ranked;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The ranked server against a one-street test level (no buildings), on a throwaway database.</summary>
public sealed class RankedServerTests : IDisposable
{
    private const string Admin = "test-admin-token-0123456789";
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ranked-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new TestClock();
    private WebApplicationFactory<Program> _factory;
    private HttpClient _http;

    public RankedServerTests() => Boot(hideRejected: false);

    private void Boot(bool hideRejected)
    {
        _http?.Dispose();
        _factory?.Dispose();
        string content = Path.Combine(_folder, "Content", "Test");
        Directory.CreateDirectory(Path.Combine(content, "Levels"));
        Directory.CreateDirectory(Path.Combine(content, "Archetypes"));
        File.WriteAllText(Path.Combine(content, "Levels", "test-01.json"),
            "{\"id\":\"test-01\",\"name\":\"TEST STREET\",\"theme\":\"Test\",\"seed\":7,\"scrollSpeed\":1.5,\"pacing\":[\"breather\"],\"props\":[\"none\"]," +
            "\"pickups\":{\"dynamite\":0,\"axe\":0,\"wreckingBall\":0,\"resurrect\":0}}");
        File.Copy(Path.Combine(FindRepo(), "Demolition Men", "Assets", "Content", "Town", "Archetypes", "shack.json"),
            Path.Combine(content, "Archetypes", "shack.json"), overwrite: true);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Ranked:Database", Path.Combine(_folder, "ranked.db"));
            b.UseSetting("Ranked:BackupFolder", Path.Combine(_folder, "backups"));
            b.UseSetting("Ranked:Content", Path.Combine(_folder, "Content"));
            b.UseSetting("Ranked:AdminToken", Admin);
            b.UseSetting("Ranked:HideRejected", hideRejected ? "true" : "false");
            b.UseSetting("Ranked:NewPlayersPerDayPerIp", "50");
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock));
        });
        _http = _factory.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private static string FindRepo()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Demolition Men")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException("the repository");
    }

    private static string Player(int n) => n.ToString("x32");

    private async Task<HttpResponseMessage> Post(string path, object body) =>
        await _http.PostAsync(path, JsonContent.Create(body, body.GetType(), options: Json));

    private async Task<T> Read<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), Json);

    private async Task<StartResponse> Start(int player, string name = "ODB", string mode = RankedApi.Solo)
    {
        HttpResponseMessage response = await Post("/start", new StartRequest { player = Player(player), name = name, mode = mode, rules = RankedRules.Version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Read<StartResponse>(response);
    }

    private LevelLayout Layout(StartResponse start) =>
        new Levels(Path.Combine(_folder, "Content")).Layout(new RankedDay { Day = start.day, Template = start.template, Seed = start.seed, Mode = RankedMode.Solo });

    // Walks from the spawn to the finish at 6 u/s: nothing in the way on the test street, so nothing scores.
    private RunLog Walk(StartResponse start, int claimed = 0, float speed = 6f)
    {
        LevelLayout layout = Layout(start);
        Assert.Empty(layout.Buildings);
        RunLog log = RunLog.For(1);
        log.Rules = RankedRules.Version;
        log.Day = start.day;
        log.Template = start.template;
        log.Seed = start.seed;
        log.LayoutHash = layout.Hash();
        float x0 = layout.SpawnX + 0.5f, x1 = layout.FinishX + 0.5f, end = (x1 - x0) / speed;
        for (int k = 0; k * RunLog.PoseInterval <= end; k++)
        {
            float x = Math.Min(x1, x0 + speed * k * RunLog.PoseInterval);
            log.Poses[0].Add(new Pose(x, layout.GroundAt((int)Math.Floor(x)), false));
        }
        int ms = (int)(end * 1000f);
        log.Events.Add(new LogEvent { TimeMs = ms, Kind = LogKind.Finish });
        log.TimeMs = ms;
        log.Score = claimed;
        return log;
    }

    private async Task<HttpResponseMessage> Submit(StartResponse start, RunLog log) =>
        await Post("/submit", new SubmitRequest { attempt = start.attempt, log = Convert.ToBase64String(Seal.Close(log.Encode(), Convert.FromBase64String(start.key))) });

    private async Task<Board> Board(int viewer = 0, string mode = RankedApi.Solo) =>
        await Read<Board>(await _http.GetAsync($"/board?mode={mode}" + (viewer > 0 ? "&player=" + Player(viewer) : "")));

    [Fact]
    public async Task Start_HandsOutAnAttemptAtTodaysLevel()
    {
        StartResponse start = await Start(1);
        Assert.Equal(20261028, start.day);
        Assert.Equal("test-01", start.template);
        Assert.Equal(32, start.attempt.Length);
        Assert.Equal(32, Convert.FromBase64String(start.key).Length);
        Assert.NotEqual((await Start(1)).key, start.key);
    }

    [Fact]
    public async Task Start_TurnsDownOldBuildsAndBadIds()
    {
        HttpResponseMessage old = await Post("/start", new StartRequest { player = Player(1), name = "ODB", mode = "solo", rules = RankedRules.Version - 1 });
        Assert.Equal((HttpStatusCode)426, old.StatusCode);
        HttpResponseMessage bad = await Post("/start", new StartRequest { player = "not-an-id", name = "ODB", mode = "solo", rules = RankedRules.Version });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        HttpResponseMessage mode = await Post("/start", new StartRequest { player = Player(1), name = "ODB", mode = "duo", rules = RankedRules.Version });
        Assert.Equal(HttpStatusCode.BadRequest, mode.StatusCode);
    }

    [Fact]
    public async Task HonestRun_LandsOnTheBoard_OnceAndUnderItsName()
    {
        StartResponse start = await Start(1, "BIG WRECKER");
        HttpResponseMessage sent = await Submit(start, Walk(start));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        SubmitResponse result = await Read<SubmitResponse>(sent);
        Assert.Equal(1, result.rank);
        Assert.Equal(1, result.of);
        Assert.True(result.best);

        Assert.Equal(HttpStatusCode.Conflict, (await Submit(start, Walk(start))).StatusCode);

        StartResponse slower = await Start(1, "BIG WRECKER");
        SubmitResponse second = await Read<SubmitResponse>(await Submit(slower, Walk(slower, speed: 5f)));
        Assert.False(second.best, "the faster run of the same score stays");

        Board board = await Board(viewer: 1);
        Assert.Equal(1, board.players);
        Assert.Single(board.entries);
        Assert.Equal("BIG WRECKER", board.entries[0].name);
        Assert.Equal(1, board.you.rank);
        Assert.Equal("test-01", board.template);
    }

    [Fact]
    public async Task OffensiveNames_BecomeTheDefault()
    {
        StartResponse start = await Start(2, "SH1THEAD");
        await Submit(start, Walk(start));
        Assert.Equal(PlayerName.Default(Player(2)), (await Board()).entries[0].name);
    }

    [Fact]
    public async Task ChangedOrForeignLogs_AreRefused()
    {
        StartResponse start = await Start(1);
        byte[] sealedLog = Seal.Close(Walk(start).Encode(), Convert.FromBase64String(start.key));
        sealedLog[5] ^= 1;
        HttpResponseMessage changed = await Post("/submit", new SubmitRequest { attempt = start.attempt, log = Convert.ToBase64String(sealedLog) });
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);

        StartResponse other = await Start(2);
        HttpResponseMessage foreign = await Post("/submit", new SubmitRequest
        {
            attempt = start.attempt, log = Convert.ToBase64String(Seal.Close(Walk(start).Encode(), Convert.FromBase64String(other.key))),
        });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post("/submit", new SubmitRequest { attempt = "nope", log = "AAAA" })).StatusCode);
    }

    [Fact]
    public async Task LateLogs_AreRefused()
    {
        StartResponse start = await Start(1);
        _clock.Now += TimeSpan.FromHours(1);
        Assert.Equal(HttpStatusCode.Gone, (await Submit(start, Walk(start))).StatusCode);
    }

    [Fact]
    public async Task RejectedRuns_ShowWhileOnlyRecorded_AndAreHiddenOnceTurnedOn()
    {
        StartResponse start = await Start(3, "CHEATER");
        SubmitResponse result = await Read<SubmitResponse>(await Submit(start, Walk(start, claimed: 99999)));
        Assert.Equal(1, result.rank); // told nothing
        Assert.Equal(99999, (await Board()).entries[0].score);

        Boot(hideRejected: true);
        Assert.Empty((await Board()).entries);
        Board own = await Board(viewer: 3);
        Assert.Equal(1, own.you.rank); // the cheater still sees it
        Assert.Equal("CHEATER", own.entries[0].name);

        StartResponse honest = await Start(4, "HONEST");
        await Submit(honest, Walk(honest));
        Board board = await Board();
        Assert.Single(board.entries);
        Assert.Equal("HONEST", board.entries[0].name);

        string review = await (await AdminGet("/admin/runs")).Content.ReadAsStringAsync();
        Assert.Contains("score: the device counted 99999", review);
    }

    [Fact]
    public async Task FarAboveTheRest_WaitsForALook()
    {
        // While rejections are only recorded the claimed scores show, which lets this test set them.
        foreach (int player in new[] { 1, 2, 3 })
        {
            StartResponse s = await Start(player);
            await Submit(s, Walk(s, claimed: 1000 * player));
        }
        StartResponse far = await Start(9, "FAR AHEAD");
        await Submit(far, Walk(far, claimed: 10000));
        Board board = await Board();
        Assert.Equal(3, board.entries.Length);
        Assert.Equal(3000, board.entries[0].score);
        Assert.Equal(1, (await Board(viewer: 9)).you.rank);

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/admin/runs")).StatusCode);
        HttpResponseMessage approve = await AdminPost($"/admin/runs/{(await Board(viewer: 9)).you.run}/approve");
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal("FAR AHEAD", (await Board()).entries[0].name);
    }

    [Fact]
    public async Task Ghost_IsTheBestVerifiedRun()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await _http.GetAsync("/ghost?mode=solo")).StatusCode);
        StartResponse start = await Start(5, "GHOST");
        await Submit(start, Walk(start));
        Ghost ghost = await Read<Ghost>(await _http.GetAsync("/ghost?mode=solo"));
        Assert.Equal("GHOST", ghost.name);
        RunLog log = RunLog.Decode(Convert.FromBase64String(ghost.log));
        Assert.NotEmpty(log.Poses[0]);
    }

    [Fact]
    public async Task Reports_AreCountedForReview()
    {
        StartResponse start = await Start(6);
        await Submit(start, Walk(start));
        int run = (await Board()).entries[0].run;
        Assert.Equal(HttpStatusCode.NoContent, (await Post("/report", new ReportRequest { player = Player(7), run = run })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post("/report", new ReportRequest { player = Player(7), run = run })).StatusCode);
        string review = await (await AdminGet("/admin/runs")).Content.ReadAsStringAsync();
        Assert.Contains("\"reports\":1", review);
    }

    [Fact]
    public async Task Coop_ShowsBothNames_OnItsOwnBoard()
    {
        HttpResponseMessage response = await Post("/start", new StartRequest
        {
            player = Player(1), name = "HOST", mode = RankedApi.Coop, partner = Player(2), partnerName = "GUEST", rules = RankedRules.Version,
        });
        StartResponse start = await Read<StartResponse>(response);
        Assert.Equal(RankedApi.Coop, start.mode);
        RunLog log = Walk(start);
        RunLog coop = RunLog.For(2);
        coop.Rules = log.Rules;
        coop.Mode = RankedMode.Coop;
        coop.Day = log.Day;
        coop.Template = log.Template;
        coop.Seed = log.Seed;
        coop.LayoutHash = log.LayoutHash;
        coop.Poses[0].AddRange(log.Poses[0]);
        foreach (Pose p in log.Poses[0])
            coop.Poses[1].Add(new Pose(p.X - 1.5f, p.Y, false));
        coop.Events.AddRange(log.Events);
        coop.TimeMs = log.TimeMs;
        await Submit(start, coop);
        Board board = await Board(mode: RankedApi.Coop);
        Assert.Equal("HOST", board.entries[0].name);
        Assert.Equal("GUEST", board.entries[0].partner);
        Assert.Empty((await Board()).entries);
    }

    [Fact]
    public async Task OldRuns_AreForgotten()
    {
        StartResponse start = await Start(8, "OLD TIMER");
        await Submit(start, Walk(start));
        _clock.Now += TimeSpan.FromDays(181);
        Boot(hideRejected: false); // the purge runs at start, as every six hours
        for (int i = 0; i < 50 && (await AdminGet("/admin/runs")).Content.ReadAsStringAsync().Result.Contains("OLD TIMER"); i++)
            await Task.Delay(100);
        Assert.DoesNotContain("OLD TIMER", await (await AdminGet("/admin/runs")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Backups_KeepTheNewestFew()
    {
        await Start(1);
        await _http.GetAsync("/health");
        string backups = Path.Combine(_folder, "backups");
        for (int i = 0; i < 50 && (!Directory.Exists(backups) || Directory.GetFiles(backups).Length == 0); i++)
            await Task.Delay(100);
        Assert.Single(Directory.GetFiles(backups, "ranked-20261028.db"));
    }

    private Task<HttpResponseMessage> AdminGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Admin);
        return _http.SendAsync(request);
    }

    private Task<HttpResponseMessage> AdminPost(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Admin);
        return _http.SendAsync(request);
    }
}

/// <summary>The campaign as shipped: every level builds on the server, the same twice.</summary>
public sealed class CampaignTests
{
    [Fact]
    public void EveryCampaignLevel_GeneratesTheSameEveryTime()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Demolition Men")))
            dir = Path.GetDirectoryName(dir);
        var levels = new Levels(Path.Combine(dir, "Demolition Men", "Assets", "Content"));
        Assert.True(levels.Templates.Count >= 12, $"{levels.Templates.Count} levels");
        foreach (string template in levels.Templates)
        {
            var day = new RankedDay { Day = 20261028, Template = template, Seed = 12345, Mode = RankedMode.Solo };
            LevelLayout layout = levels.Layout(day);
            Assert.NotNull(layout);
            Assert.NotEmpty(layout.Buildings);
            Assert.Equal(layout.Hash(), new Levels(Path.Combine(dir, "Demolition Men", "Assets", "Content")).Layout(day).Hash());
        }
        Assert.Contains(RankedDay.Pick(DateTime.UtcNow, levels.Templates, RankedMode.Solo).Template, levels.Templates);
    }
}
