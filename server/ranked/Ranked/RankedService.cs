using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Demolition.Levels;
using Demolition.Ranked;
using Demolition.Run;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class RankedOptions
{
    public string Database { get; set; } = "/data/ranked.db";
    /// <summary>The campaign's level and archetype JSON; empty: the Content folder shipped next to the app.</summary>
    public string Content { get; set; } = "";
    /// <summary>Where the daily copies of the database go, and how many are kept; empty: none.</summary>
    public string BackupFolder { get; set; } = "/data/backups";
    public int BackupsKept { get; set; } = 7;
    /// <summary>Runs, attempts, reports and players with no run left are forgotten after this many
    /// days (the privacy policy says so: change both together).</summary>
    public int RetentionDays { get; set; } = 180;
    /// <summary>docs/ranked.md K-D5: a turned-down run (and its player) is seen only by that player.
    /// Off for the first weeks: every run shows, and the reasons are only recorded.</summary>
    public bool HideRejected { get; set; }
    /// <summary>A score this many times the day's best waits for a look (admin approve) before others see it.</summary>
    public float HoldFactor { get; set; } = 2f;
    public int HoldMinEntries { get; set; } = 3;
    public int AttemptsPerDay { get; set; } = 200;
    public int StartsPerMinutePerIp { get; set; } = 30;
    public int NewPlayersPerDayPerIp { get; set; } = 5;
    public int ReportsPerDayPerIp { get; set; } = 30;
    public TimeSpan AttemptLifetime { get; set; } = TimeSpan.FromMinutes(40);
    public int MaxLogBytes { get; set; } = 256 * 1024;
    public int BoardSize { get; set; } = 50;
    /// <summary>Bearer token for /admin; empty turns /admin off.</summary>
    public string AdminToken { get; set; } = "";
    /// <summary>CIDRs whose X-Forwarded-For is believed, besides loopback (the Caddy container's network).</summary>
    public string[] TrustedProxyNetworks { get; set; } = [];
}

/// <summary>
/// The referee's office (docs/ranked.md §2, §4, §5): hands out attempts with their keys, opens and
/// replays the logs that come back, keeps the boards.
/// </summary>
public sealed class RankedService
{
    private readonly RankedOptions _options;
    private readonly Levels _levels;
    private readonly Store _store;
    private readonly Limits _limits = new Limits();
    private readonly TimeProvider _time;
    private readonly ILogger _log;

    public RankedService(RankedOptions options, Levels levels, Store store, TimeProvider time, ILogger log)
    {
        _options = options;
        _levels = levels;
        _store = store;
        _time = time;
        _log = log;
    }

    /// <summary>The game's clock only ever runs behind the real one (pauses, hit-stop); this is the two machines' timing.</summary>
    private const long WallClockSlackMs = 2000;

    private DateTimeOffset Now => _time.GetUtcNow();

    public RankedDay Today(RankedMode mode) => RankedDay.Pick(Now.UtcDateTime, _levels.Templates, mode);

    private static IResult Error(int status, string error) => Results.Json(new Dictionary<string, string> { ["error"] = error }, statusCode: status);

    // ---------------- Attempts ----------------

    public IResult Start(StartRequest request, string ip)
    {
        if (request == null || !RankedApi.IsPlayerId(request.player) || !RankedApi.TryMode(request.mode, out RankedMode mode))
            return Error(400, "bad request");
        if (request.rules != RankedRules.Version)
            return Error(426, "update"); // another build's rules: the game says to update
        bool partner = mode == RankedMode.Coop && !string.IsNullOrEmpty(request.partner);
        if (partner && (!RankedApi.IsPlayerId(request.partner) || request.partner == request.player))
            return Error(400, "bad request");
        if (!_limits.Allow("start", ip, _options.StartsPerMinutePerIp, TimeSpan.FromMinutes(1), Now))
            return Error(429, "slow down");

        long now = Now.ToUnixTimeMilliseconds();
        foreach ((string id, string typed) in partner
                     ? new[] { (request.player, request.name), (request.partner, request.partnerName) }
                     : new[] { (request.player, request.name) })
        {
            string name = PlayerName.Clean(typed);
            if (!PlayerName.Allowed(name))
                name = PlayerName.Default(id); // the game checks too: only a changed one sends these
            if (_store.Player(id) == null && !_limits.Allow("new", ip, _options.NewPlayersPerDayPerIp, TimeSpan.FromDays(1), Now))
                return Error(429, "too many new players");
            _store.SavePlayer(id, name, now);
        }

        RankedDay day = Today(mode);
        if (_store.Attempts(request.player, day.Day) >= _options.AttemptsPerDay)
            return Error(429, "enough for today");
        var attempt = new AttemptRow
        {
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            Player = request.player,
            Partner = partner ? request.partner : null,
            Mode = (int)mode,
            Day = day.Day,
            Template = day.Template,
            Seed = day.Seed,
            Key = RandomNumberGenerator.GetBytes(32),
            Created = now,
        };
        _store.AddAttempt(attempt);
        return Results.Json(new StartResponse
        {
            attempt = attempt.Id, key = Convert.ToBase64String(attempt.Key), day = day.Day, template = day.Template, seed = day.Seed,
            mode = RankedApi.ModeName(mode),
        });
    }

    public IResult Submit(SubmitRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.attempt) || string.IsNullOrEmpty(request.log)
            || request.log.Length > _options.MaxLogBytes * 4 / 3 + 4)
            return Error(400, "bad request");
        AttemptRow attempt = _store.Attempt(request.attempt);
        if (attempt == null)
            return Error(404, "no such attempt");
        long now = Now.ToUnixTimeMilliseconds();
        if (now - attempt.Created > (long)_options.AttemptLifetime.TotalMilliseconds)
            return Error(410, "too late");
        byte[] sealedLog;
        try
        {
            sealedLog = Convert.FromBase64String(request.log);
        }
        catch (FormatException)
        {
            return Error(400, "bad request");
        }
        byte[] plain = Seal.Open(sealedLog, attempt.Key);
        if (plain == null)
            return Error(400, "bad request"); // not sealed with this attempt's key, or changed since
        RunLog log;
        try
        {
            log = RunLog.Decode(plain);
        }
        catch (FormatException)
        {
            return Error(400, "bad request");
        }
        if (!_store.UseAttempt(attempt.Id))
            return Error(409, "already sent");

        var mode = (RankedMode)attempt.Mode;
        var day = new RankedDay { Day = attempt.Day, Template = attempt.Template, Seed = attempt.Seed, Mode = mode };
        Verdict verdict;
        if (log.Day != day.Day || log.Template != day.Template || log.Seed != day.Seed || log.Mode != mode)
            verdict = new Verdict { Reason = "another day, level or mode than the attempt's" };
        else if (log.TimeMs > now - attempt.Created + WallClockSlackMs)
            verdict = new Verdict { Reason = "a run longer than the time since its attempt began" };
        else if (_store.HasLog(day.Day, attempt.Mode, plain))
            verdict = new Verdict { Reason = "a copy of a run already sent" };
        else
        {
            LevelLayout layout = _levels.Layout(day);
            verdict = layout == null ? new Verdict { Reason = "unknown level " + day.Template } : RunVerifier.Verify(log, layout);
        }

        bool finished = log.Events.Count > 0 && log.Events[log.Events.Count - 1].Kind == LogKind.Finish;
        var run = new RunRow
        {
            Player = attempt.Player, Partner = attempt.Partner, Mode = attempt.Mode, Day = attempt.Day, Ok = verdict.Ok,
            Finished = verdict.Ok ? verdict.Finished : finished, Score = verdict.Ok ? verdict.Score : log.Score, Claimed = log.Score,
            TimeMs = log.TimeMs, Destroyed = verdict.Destroyed, Reason = verdict.Reason, Event = verdict.Event, Created = now,
        };
        if (!verdict.Ok)
        {
            _log.LogWarning("Run of {Player} on {Day} {Mode} turned down: {Reason} (event {Event})",
                attempt.Player, day.Day, RankedApi.ModeName(mode), verdict.Reason, verdict.Event);
            if (_options.HideRejected)
                _store.HidePlayer(attempt.Player, true);
        }
        if (run.Finished && Shown(run, null, _store.Players(new[] { run.Player })))
        {
            List<RunRow> best = BestPerPlayer(day.Day, attempt.Mode, null);
            run.Held = best.Count >= _options.HoldMinEntries && run.Score > best[0].Score * _options.HoldFactor;
        }
        run.Id = _store.AddRun(run, attempt.Id, plain);

        var response = new SubmitResponse { score = run.Score };
        if (run.Finished)
        {
            List<RunRow> board = BestPerPlayer(day.Day, attempt.Mode, attempt.Player);
            response.of = board.Count;
            for (int i = 0; i < board.Count; i++)
                if (board[i].Player == attempt.Player)
                {
                    response.rank = i + 1;
                    response.best = board[i].Id == run.Id;
                }
        }
        return Results.Json(response);
    }

    // ---------------- Boards ----------------

    // Others see a run once it's shown: verified (or, while rejections are only recorded, any),
    // not held for a look, its player not hidden. Its own player always sees it.
    private bool Shown(RunRow run, string viewer, Dictionary<string, PlayerRow> players)
    {
        if (run.Player == viewer)
            return true;
        if ((run.Held && !run.Approved) || (players.TryGetValue(run.Player, out PlayerRow p) && p.Hidden))
            return false;
        return run.Ok || !_options.HideRejected;
    }

    /// <summary>Each player's best shown run of the day on one board, best first.</summary>
    private List<RunRow> BestPerPlayer(int day, int mode, string viewer)
    {
        List<RunRow> runs = _store.FinishedRuns(day, mode);
        var ids = new List<string>();
        foreach (RunRow r in runs)
            ids.Add(r.Player);
        Dictionary<string, PlayerRow> players = _store.Players(ids);
        var best = new Dictionary<string, RunRow>();
        foreach (RunRow r in runs)
            if (Shown(r, viewer, players) && (!best.TryGetValue(r.Player, out RunRow b) || Better(r, b)))
                best[r.Player] = r;
        var list = new List<RunRow>(best.Values);
        list.Sort((a, b) => Better(a, b) ? -1 : Better(b, a) ? 1 : 0);
        return list;
    }

    private static bool Better(RunRow a, RunRow b) =>
        a.Score != b.Score ? a.Score > b.Score : a.TimeMs != b.TimeMs ? a.TimeMs < b.TimeMs : a.Id < b.Id;

    public IResult Board(int day, string modeName, string viewer)
    {
        if (!RankedApi.TryMode(modeName, out RankedMode mode))
            return Error(400, "bad request");
        RankedDay today = Today(mode);
        if (day == 0)
            day = today.Day;
        if (viewer != null && !RankedApi.IsPlayerId(viewer))
            viewer = null;
        List<RunRow> best = BestPerPlayer(day, (int)mode, viewer);
        var ids = new List<string>();
        foreach (RunRow r in best)
        {
            ids.Add(r.Player);
            ids.Add(r.Partner);
        }
        Dictionary<string, PlayerRow> players = _store.Players(ids);
        var board = new Board
        {
            day = day, mode = RankedApi.ModeName(mode), players = best.Count,
            template = day == today.Day ? today.Template : string.Empty, seed = day == today.Day ? today.Seed : 0,
        };
        var entries = new List<BoardEntry>();
        for (int i = 0; i < best.Count; i++)
        {
            BoardEntry entry = Entry(best[i], i + 1, players);
            if (i < _options.BoardSize)
                entries.Add(entry);
            if (best[i].Player == viewer)
                board.you = entry;
        }
        board.entries = entries.ToArray();
        return Results.Json(board);
    }

    private static BoardEntry Entry(RunRow run, int rank, Dictionary<string, PlayerRow> players) => new BoardEntry
    {
        run = (int)run.Id, rank = rank, score = run.Score, timeMs = run.TimeMs,
        stars = RunScore.Stars(RunScore.GradeFor(run.Destroyed)),
        name = players.TryGetValue(run.Player, out PlayerRow p) ? p.Name : PlayerName.Default(run.Player),
        partner = run.Partner == null ? string.Empty : players.TryGetValue(run.Partner, out PlayerRow q) ? q.Name : PlayerName.Default(run.Partner),
    };

    /// <summary>The best verified run of the day, to race: its log, unsealed.</summary>
    public IResult Ghost(int day, string modeName)
    {
        if (!RankedApi.TryMode(modeName, out RankedMode mode))
            return Error(400, "bad request");
        if (day == 0)
            day = Today(mode).Day;
        foreach (RunRow run in BestPerPlayer(day, (int)mode, null))
            if (run.Ok)
            {
                PlayerRow player = _store.Player(run.Player);
                return Results.Json(new Ghost
                {
                    run = (int)run.Id, name = player != null ? player.Name : PlayerName.Default(run.Player), score = run.Score,
                    log = Convert.ToBase64String(_store.Log(run.Id)),
                });
            }
        return Results.NoContent();
    }

    public IResult Report(ReportRequest request, string ip)
    {
        if (request == null || !RankedApi.IsPlayerId(request.player) || request.run <= 0 || _store.RunById(request.run) == null)
            return Error(400, "bad request");
        if (!_limits.Allow("report", ip, _options.ReportsPerDayPerIp, TimeSpan.FromDays(1), Now))
            return Error(429, "slow down");
        if (_store.Report(request.run, request.player, Now.ToUnixTimeMilliseconds()))
            _log.LogWarning("Run {Run} reported", request.run);
        return Results.NoContent();
    }

    // ---------------- Review (/admin) ----------------

    /// <summary>The latest runs with the replay's verdict, reports and holds: what to look at.</summary>
    public IResult Review(int limit)
    {
        var rows = new List<object>();
        List<RunRow> runs = _store.RecentRuns(Math.Clamp(limit, 1, 1000));
        var ids = new List<string>();
        foreach (RunRow r in runs)
            ids.Add(r.Player);
        Dictionary<string, PlayerRow> players = _store.Players(ids);
        foreach (RunRow r in runs)
            rows.Add(new
            {
                run = r.Id, day = r.Day, mode = RankedApi.ModeName((RankedMode)r.Mode), player = r.Player,
                name = players.TryGetValue(r.Player, out PlayerRow p) ? p.Name : "", hidden = p != null && p.Hidden,
                ok = r.Ok, reason = r.Reason, @event = r.Event, finished = r.Finished, held = r.Held && !r.Approved,
                score = r.Score, claimed = r.Claimed, timeMs = r.TimeMs, reports = _store.Reports(r.Id),
            });
        return Results.Json(rows);
    }

    public IResult Approve(long run) => _store.Approve(run) ? Results.NoContent() : Results.NotFound();

    public IResult Hide(string player, bool hidden)
    {
        if (!RankedApi.IsPlayerId(player) || _store.Player(player) == null)
            return Results.NotFound();
        _store.HidePlayer(player, hidden);
        return Results.NoContent();
    }

    /// <summary>Forget what's past <see cref="RankedOptions.RetentionDays"/>.</summary>
    public void Purge()
    {
        if (_options.RetentionDays <= 0)
            return;
        int runs = _store.Purge((Now - TimeSpan.FromDays(_options.RetentionDays)).ToUnixTimeMilliseconds());
        if (runs > 0)
            _log.LogInformation("Forgot {Runs} runs older than {Days} days", runs, _options.RetentionDays);
    }

    /// <summary>Today's copy of the database, and only the newest <see cref="RankedOptions.BackupsKept"/>.</summary>
    public void Backup()
    {
        if (string.IsNullOrEmpty(_options.BackupFolder))
            return;
        System.IO.Directory.CreateDirectory(_options.BackupFolder);
        string path = System.IO.Path.Combine(_options.BackupFolder, $"ranked-{Now.UtcDateTime:yyyyMMdd}.db");
        _store.Backup(path);
        var files = new List<string>(System.IO.Directory.GetFiles(_options.BackupFolder, "ranked-*.db"));
        files.Sort(string.CompareOrdinal);
        for (int i = 0; i < files.Count - _options.BackupsKept; i++)
            System.IO.File.Delete(files[i]);
    }
}
