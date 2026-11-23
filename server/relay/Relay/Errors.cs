using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;

/// One exception as the game posts it (Meta/Model/ErrorReports.cs): the game's version, its platform,
/// the device's model, the exception's message and its stack trace, both cut short and with the
/// device's own folders taken out on the device.
public sealed record ErrorReport(string? V, string? P, string? Device, string? Msg, string? Stack);

/// One kind of error: the same exception type and stack trace, in one version on one platform.
public sealed record ErrorRow(string Version, string Platform, string Message, string Stack, string Device,
    string FirstDay, string LastDay, long N);

/// The game's error reports: C# exceptions that don't crash the app (Android vitals sees only real
/// crashes and freezes). POST /error adds one to a row per (exception type and stack trace, version,
/// platform) in a SQLite file, which keeps the latest message and device model of each row as a
/// sample. No address, no user agent, no id, no time of day. Anyone can post, so a row is a lead to
/// look at, never proof.
public sealed partial class Errors : IDisposable
{
    public const int MaxMessage = 500, MaxStack = 4000, MaxDevice = 64;
    const int MaxBody = 8192; // the game's longest: a 500 and a 4000 character field, escaped
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [GeneratedRegex(@"^[0-9A-Za-z._+-]{1,32}\z")]
    private static partial Regex VersionText();

    readonly SqliteConnection db;
    readonly int maxRows, keepDays;
    readonly PartitionedRateLimiter<string> posts;

    public Errors(RelayOptions opt)
    {
        maxRows = opt.ErrorRows;
        keepDays = opt.ErrorDays;
        posts = PartitionedRateLimiter.Create<string, string>(ip => RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = opt.ErrorsPerMinute, Window = TimeSpan.FromMinutes(1) }));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opt.ErrorsDatabase))!);
        db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = opt.ErrorsDatabase, Pooling = false }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS errors (sig TEXT NOT NULL, version TEXT NOT NULL, platform TEXT NOT NULL,
              message TEXT NOT NULL, stack TEXT NOT NULL, device TEXT NOT NULL,
              first_day TEXT NOT NULL, last_day TEXT NOT NULL, n INTEGER NOT NULL,
              PRIMARY KEY (sig, version, platform)) WITHOUT ROWID;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => db.Dispose();

    /// What the game sends: its version and platform, and printable text within the caps.
    public static bool Valid(ErrorReport r) =>
        r.V != null && VersionText().IsMatch(r.V)
        && r.P is "web" or "android" or "pc" or "other"
        && Text(r.Msg, 1, MaxMessage) && Text(r.Stack, 0, MaxStack) && Text(r.Device, 0, MaxDevice)
        && !r.Device!.Contains('\n');

    static bool Text(string? s, int min, int max) =>
        s != null && s.Length >= min && s.Length <= max && s.All(c => c >= ' ' || c is '\n' or '\t');

    /// Which rows are the same error: the exception's type (the message up to its first colon) and
    /// its stack trace. The rest of the message varies (an index, a name) and would split one bug in many.
    public static string Signature(string message, string stack)
    {
        int colon = message.IndexOf(':');
        string type = colon > 0 ? message[..colon] : message;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(type + "\n" + stack)))[..32].ToLowerInvariant();
    }

    /// POST /error: the answer is a status and no body.
    public async Task Post(HttpContext ctx) => ctx.Response.StatusCode = await Take(ctx);

    async Task<int> Take(HttpContext ctx)
    {
        // The address is the limiter's key, in memory for a minute, and goes nowhere else.
        if (!posts.AttemptAcquire(ctx.Connection.RemoteIpAddress?.ToString() ?? "").IsAcquired) return 429;
        var body = new byte[MaxBody + 1];
        int length = 0, read;
        while (length < body.Length && (read = await ctx.Request.Body.ReadAsync(body.AsMemory(length), ctx.RequestAborted)) > 0)
            length += read;
        if (length > MaxBody) return 413;
        ErrorReport? r;
        try { r = JsonSerializer.Deserialize<ErrorReport>(body.AsSpan(0, length), Json); }
        catch (JsonException) { return 400; }
        if (r == null || !Valid(r)) return 400;
        return Add(r) ? 204 : 503;
    }

    bool Add(ErrorReport r)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        try
        {
            lock (db)
            {
                // Old rows go first, then a new row only while the file has room for one.
                using var cmd = Command("""
                    DELETE FROM errors WHERE last_day < $oldest;
                    UPDATE errors SET n = n + 1, last_day = $day, message = $msg, device = $device
                      WHERE sig = $sig AND version = $v AND platform = $p;
                    INSERT INTO errors (sig, version, platform, message, stack, device, first_day, last_day, n)
                      SELECT $sig, $v, $p, $msg, $stack, $device, $day, $day, 1
                      WHERE changes() = 0 AND (SELECT COUNT(*) FROM errors) < $max;
                    """,
                    ("$oldest", DateTime.UtcNow.AddDays(-keepDays).ToString("yyyy-MM-dd")), ("$day", today),
                    ("$sig", Signature(r.Msg!, r.Stack!)), ("$v", r.V!), ("$p", r.P!), ("$msg", r.Msg!), ("$stack", r.Stack!),
                    ("$device", r.Device!), ("$max", maxRows));
                cmd.ExecuteNonQuery();
            }
            return true;
        }
        catch (SqliteException) { return false; } // a full disk loses reports, never a co-op session
    }

    /// Every row seen on or after the day <paramref name="from"/> (yyyy-MM-dd), the most frequent first.
    public List<ErrorRow> Read(string from)
    {
        var rows = new List<ErrorRow>();
        lock (db)
        {
            using var cmd = Command("""
                SELECT version, platform, message, stack, device, first_day, last_day, n FROM errors
                WHERE last_day >= $from ORDER BY n DESC, last_day DESC
                """, ("$from", from));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                rows.Add(new ErrorRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7)));
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
