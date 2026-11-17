using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;

/// One count as the game posts it (docs/hit-plan.md §5.1, F0). The game's JsonUtility writes every
/// field every time: "" and 0 where an event has none.
public sealed record CountEvent(string? E, string? P, string? Level, int Stars, int Players, int Online, int Since);

/// One counter: how often this event came in on this UTC day with these fields.
public sealed record CountRow(string Day, string Event, string Platform, string Level, int Stars, int Players, int Online, int Since, long N);

/// The game's anonymous counters. POST /count adds one to a counter per (UTC day, event, fields) in a
/// SQLite file, and that is all it keeps: no address, no user agent, no id, no time of day.
/// Anyone can post, so the numbers are for deciding what to build, never for money.
public sealed partial class Counters : IDisposable
{
    const int MaxBody = 256; // the longest event the game sends is about 110 bytes
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // a field we don't know is a reason to refuse, not to store
    };

    // \z, not $: $ also matches before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9-]{1,24}\z")]
    private static partial Regex LevelId();

    readonly SqliteConnection db;
    readonly int maxRowsPerDay;
    readonly PartitionedRateLimiter<string> posts;

    public Counters(RelayOptions opt)
    {
        maxRowsPerDay = opt.CountRowsPerDay;
        posts = PartitionedRateLimiter.Create<string, string>(ip => RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = opt.CountsPerMinute, Window = TimeSpan.FromMinutes(1) }));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opt.CountersDatabase))!);
        db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = opt.CountersDatabase, Pooling = false }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        // WAL with synchronous=NORMAL: no disk sync per count. A power cut may lose the last few.
        cmd.CommandText = """
            PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS counts (day TEXT NOT NULL, event TEXT NOT NULL, platform TEXT NOT NULL, level TEXT NOT NULL,
              stars INTEGER NOT NULL, players INTEGER NOT NULL, online INTEGER NOT NULL, since INTEGER NOT NULL, n INTEGER NOT NULL,
              PRIMARY KEY (day, event, platform, level, stars, players, online, since)) WITHOUT ROWID;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => db.Dispose();

    /// The fixed list: an event the game sends, and only that event's own fields set.
    public static bool Valid(CountEvent c)
    {
        if (c.P is not ("web" or "android" or "pc" or "other")) return false;
        var bare = new CountEvent(c.E, c.P, "", 0, 0, 0, 0);
        return c.E switch
        {
            "first_start" or "tutorial_done" or "share" or "invite" => c == bare,
            "day" => c == bare with { Since = c.Since } && c.Since is 0 or 1 or 7 or 30,
            "level_done" => c == bare with { Level = c.Level, Stars = c.Stars, Players = c.Players, Online = c.Online }
                && LevelId().IsMatch(c.Level!) && c.Stars is >= 0 and <= 3 && c.Players is 1 or 2 && c.Online is >= 0 and <= 2,
            _ => false,
        };
    }

    /// POST /count: the answer is a status and no body.
    public async Task Post(HttpContext ctx) => ctx.Response.StatusCode = await Take(ctx);

    async Task<int> Take(HttpContext ctx)
    {
        // The address is the limiter's key, in memory for a minute, and goes nowhere else.
        if (!posts.AttemptAcquire(ctx.Connection.RemoteIpAddress?.ToString() ?? "").IsAcquired) return 429;
        // Read by hand, so the cap holds whatever Content-Length says.
        var body = new byte[MaxBody + 1];
        int length = 0, read;
        while (length < body.Length && (read = await ctx.Request.Body.ReadAsync(body.AsMemory(length), ctx.RequestAborted)) > 0)
            length += read;
        if (length > MaxBody) return 413;
        CountEvent? c;
        try { c = JsonSerializer.Deserialize<CountEvent>(body.AsSpan(0, length), Json); }
        catch (JsonException) { return 400; }
        if (c == null) return 400;
        c = c with { Level = c.Level ?? "" };
        if (!Valid(c)) return 400;
        return Add(c.E!, c.P!, c.Level!, c.Stars, c.Players, c.Online, c.Since) ? 204 : 503;
    }

    /// The relay's own counts: a room opened, a joiner paired.
    public void Count(string name, string platform) => Add(name, platform, "", 0, 0, 0, 0);

    bool Add(string name, string platform, string level, int stars, int players, int online, int since)
    {
        try
        {
            lock (db)
            {
                // A new row only while the day has room for one: junk can't grow the file without end.
                using var cmd = Command("""
                    UPDATE counts SET n = n + 1 WHERE day = $day AND event = $e AND platform = $p AND level = $level
                      AND stars = $stars AND players = $players AND online = $online AND since = $since;
                    INSERT INTO counts (day, event, platform, level, stars, players, online, since, n)
                      SELECT $day, $e, $p, $level, $stars, $players, $online, $since, 1
                      WHERE changes() = 0 AND (SELECT COUNT(*) FROM counts WHERE day = $day) < $max;
                    """,
                    ("$day", DateTime.UtcNow.ToString("yyyy-MM-dd")), ("$e", name), ("$p", platform), ("$level", level), ("$stars", stars),
                    ("$players", players), ("$online", online), ("$since", since), ("$max", maxRowsPerDay));
                cmd.ExecuteNonQuery();
            }
            return true;
        }
        catch (SqliteException) { return false; } // a full disk loses counts, never a co-op session
    }

    /// Every counter of the days from..to (yyyy-MM-dd, inclusive).
    public List<CountRow> Read(string from, string to)
    {
        var rows = new List<CountRow>();
        lock (db)
        {
            using var cmd = Command("""
                SELECT day, event, platform, level, stars, players, online, since, n FROM counts
                WHERE day >= $from AND day <= $to ORDER BY day, event, platform, level, stars, players, online, since
                """, ("$from", from), ("$to", to));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add(new CountRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5),
                    r.GetInt32(6), r.GetInt32(7), r.GetInt64(8)));
        }
        return rows;
    }

    SqliteCommand Command(string sql, params (string Name, object Value)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }
}
