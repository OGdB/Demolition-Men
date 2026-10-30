using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

/// <summary>A stored run: what the device claimed, what the replay found, and the log for its ghost.</summary>
public sealed class RunRow
{
    public long Id;
    public string Player, Partner;
    public int Mode, Day;
    public bool Ok, Finished, Held, Approved;
    public int Score, Claimed, TimeMs;
    public float Destroyed;
    public string Reason;
    public int Event;
    public long Created;
}

public sealed class PlayerRow
{
    public string Id, Name;
    public bool Hidden;
}

public sealed class AttemptRow
{
    public string Id, Player, Partner, Template;
    public int Mode, Day, Seed;
    public byte[] Key;
    public long Created;
    public bool Used;
}

/// <summary>
/// The ranked server's SQLite file (docs/ranked.md §8): players, attempts, runs, reports. One
/// connection, one caller at a time: the traffic is a handful of requests a second at most.
/// </summary>
public sealed class Store : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly object _lock = new object();

    public Store(string path)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL;");
        Exec(@"CREATE TABLE IF NOT EXISTS players (id TEXT PRIMARY KEY, name TEXT NOT NULL, created INTEGER NOT NULL, hidden INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS attempts (id TEXT PRIMARY KEY, player TEXT NOT NULL, partner TEXT, mode INTEGER NOT NULL, day INTEGER NOT NULL,
  template TEXT NOT NULL, seed INTEGER NOT NULL, key BLOB NOT NULL, created INTEGER NOT NULL, used INTEGER NOT NULL DEFAULT 0);
CREATE INDEX IF NOT EXISTS attempts_player ON attempts (player, day);
CREATE TABLE IF NOT EXISTS runs (id INTEGER PRIMARY KEY AUTOINCREMENT, attempt TEXT NOT NULL, player TEXT NOT NULL, partner TEXT,
  mode INTEGER NOT NULL, day INTEGER NOT NULL, ok INTEGER NOT NULL, finished INTEGER NOT NULL, score INTEGER NOT NULL,
  claimed INTEGER NOT NULL, time_ms INTEGER NOT NULL, destroyed REAL NOT NULL, reason TEXT NOT NULL, event INTEGER NOT NULL,
  held INTEGER NOT NULL DEFAULT 0, approved INTEGER NOT NULL DEFAULT 0, log BLOB NOT NULL, created INTEGER NOT NULL);
CREATE INDEX IF NOT EXISTS runs_board ON runs (day, mode);
CREATE TABLE IF NOT EXISTS reports (run INTEGER NOT NULL, reporter TEXT NOT NULL, created INTEGER NOT NULL, PRIMARY KEY (run, reporter));");
    }

    public void Dispose() => _db.Dispose();

    private void Exec(string sql)
    {
        using SqliteCommand cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private SqliteCommand Command(string sql, params (string, object)[] args)
    {
        SqliteCommand cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        foreach ((string name, object value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private int Run(string sql, params (string, object)[] args)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command(sql, args);
            return cmd.ExecuteNonQuery();
        }
    }

    private object Scalar(string sql, params (string, object)[] args)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command(sql, args);
            return cmd.ExecuteScalar();
        }
    }

    // ---------------- Players ----------------

    public PlayerRow Player(string id)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command("SELECT name, hidden FROM players WHERE id = $id", ("$id", id));
            using SqliteDataReader r = cmd.ExecuteReader();
            return r.Read() ? new PlayerRow { Id = id, Name = r.GetString(0), Hidden = r.GetInt64(1) != 0 } : null;
        }
    }

    public void SavePlayer(string id, string name, long now) =>
        Run("INSERT INTO players (id, name, created) VALUES ($id, $name, $now) ON CONFLICT(id) DO UPDATE SET name = $name",
            ("$id", id), ("$name", name), ("$now", now));

    public void HidePlayer(string id, bool hidden) =>
        Run("UPDATE players SET hidden = $h WHERE id = $id", ("$id", id), ("$h", hidden ? 1 : 0));

    public Dictionary<string, PlayerRow> Players(IEnumerable<string> ids)
    {
        var found = new Dictionary<string, PlayerRow>();
        foreach (string id in ids)
            if (id != null && !found.ContainsKey(id))
            {
                PlayerRow p = Player(id);
                if (p != null)
                    found[id] = p;
            }
        return found;
    }

    // ---------------- Attempts ----------------

    public void AddAttempt(AttemptRow a) =>
        Run("INSERT INTO attempts (id, player, partner, mode, day, template, seed, key, created) VALUES ($id, $p, $q, $m, $d, $t, $s, $k, $c)",
            ("$id", a.Id), ("$p", a.Player), ("$q", a.Partner), ("$m", a.Mode), ("$d", a.Day), ("$t", a.Template), ("$s", a.Seed),
            ("$k", a.Key), ("$c", a.Created));

    public int Attempts(string player, int day) =>
        Convert.ToInt32(Scalar("SELECT COUNT(*) FROM attempts WHERE player = $p AND day = $d", ("$p", player), ("$d", day)));

    public AttemptRow Attempt(string id)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command("SELECT player, partner, mode, day, template, seed, key, created, used FROM attempts WHERE id = $id", ("$id", id));
            using SqliteDataReader r = cmd.ExecuteReader();
            if (!r.Read())
                return null;
            return new AttemptRow
            {
                Id = id, Player = r.GetString(0), Partner = r.IsDBNull(1) ? null : r.GetString(1), Mode = r.GetInt32(2), Day = r.GetInt32(3),
                Template = r.GetString(4), Seed = r.GetInt32(5), Key = (byte[])r[6], Created = r.GetInt64(7), Used = r.GetInt64(8) != 0,
            };
        }
    }

    /// <summary>Mark it used; false if it already was (a second submit of the same attempt).</summary>
    public bool UseAttempt(string id) => Run("UPDATE attempts SET used = 1 WHERE id = $id AND used = 0", ("$id", id)) == 1;

    // ---------------- Runs ----------------

    public long AddRun(RunRow run, string attempt, byte[] log)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command(@"INSERT INTO runs (attempt, player, partner, mode, day, ok, finished, score, claimed, time_ms, destroyed,
  reason, event, held, log, created) VALUES ($a, $p, $q, $m, $d, $ok, $f, $s, $c, $t, $x, $r, $e, $h, $log, $now); SELECT last_insert_rowid();",
                ("$a", attempt), ("$p", run.Player), ("$q", run.Partner), ("$m", run.Mode), ("$d", run.Day), ("$ok", run.Ok ? 1 : 0),
                ("$f", run.Finished ? 1 : 0), ("$s", run.Score), ("$c", run.Claimed), ("$t", run.TimeMs), ("$x", run.Destroyed),
                ("$r", run.Reason ?? string.Empty), ("$e", run.Event), ("$h", run.Held ? 1 : 0), ("$log", log), ("$now", run.Created));
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }

    private const string RunColumns = "id, player, partner, mode, day, ok, finished, held, approved, score, claimed, time_ms, destroyed, reason, event, created";

    private static RunRow ReadRun(SqliteDataReader r) => new RunRow
    {
        Id = r.GetInt64(0), Player = r.GetString(1), Partner = r.IsDBNull(2) ? null : r.GetString(2), Mode = r.GetInt32(3), Day = r.GetInt32(4),
        Ok = r.GetInt64(5) != 0, Finished = r.GetInt64(6) != 0, Held = r.GetInt64(7) != 0, Approved = r.GetInt64(8) != 0, Score = r.GetInt32(9),
        Claimed = r.GetInt32(10), TimeMs = r.GetInt32(11), Destroyed = (float)r.GetDouble(12), Reason = r.GetString(13), Event = r.GetInt32(14),
        Created = r.GetInt64(15),
    };

    /// <summary>The day's finished runs on one board.</summary>
    public List<RunRow> FinishedRuns(int day, int mode) =>
        Runs($"SELECT {RunColumns} FROM runs WHERE day = $d AND mode = $m AND finished = 1", ("$d", day), ("$m", mode));

    public List<RunRow> RecentRuns(int limit) =>
        Runs($"SELECT {RunColumns} FROM runs ORDER BY id DESC LIMIT $n", ("$n", limit));

    public RunRow RunById(long id)
    {
        List<RunRow> runs = Runs($"SELECT {RunColumns} FROM runs WHERE id = $id", ("$id", id));
        return runs.Count > 0 ? runs[0] : null;
    }

    private List<RunRow> Runs(string sql, params (string, object)[] args)
    {
        var runs = new List<RunRow>();
        lock (_lock)
        {
            using SqliteCommand cmd = Command(sql, args);
            using SqliteDataReader r = cmd.ExecuteReader();
            while (r.Read())
                runs.Add(ReadRun(r));
        }
        return runs;
    }

    /// <summary>The same log, to the byte, was already sent for this board.</summary>
    public bool HasLog(int day, int mode, byte[] log) =>
        Scalar("SELECT 1 FROM runs WHERE day = $d AND mode = $m AND log = $l LIMIT 1", ("$d", day), ("$m", mode), ("$l", log)) != null;

    public byte[] Log(long run) => Scalar("SELECT log FROM runs WHERE id = $id", ("$id", run)) as byte[];

    public bool Approve(long run) => Run("UPDATE runs SET held = 0, approved = 1 WHERE id = $id", ("$id", run)) == 1;

    // ---------------- Reports ----------------

    public bool Report(long run, string reporter, long now) =>
        Run("INSERT OR IGNORE INTO reports (run, reporter, created) VALUES ($r, $p, $now)", ("$r", run), ("$p", reporter), ("$now", now)) == 1;

    public int Reports(long run) => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM reports WHERE run = $r", ("$r", run)));

    // ---------------- Keeping ----------------

    /// <summary>Forget what's older than <paramref name="before"/> (ms): runs, attempts, reports, and
    /// players who have no run left. Returns the runs forgotten.</summary>
    public int Purge(long before)
    {
        lock (_lock)
        {
            using SqliteCommand cmd = Command(@"DELETE FROM reports WHERE created < $c OR run IN (SELECT id FROM runs WHERE created < $c);
DELETE FROM attempts WHERE created < $c;
DELETE FROM runs WHERE created < $c;
SELECT changes();
DELETE FROM players WHERE created < $c AND id NOT IN (SELECT player FROM runs) AND id NOT IN (SELECT partner FROM runs WHERE partner IS NOT NULL)
  AND id NOT IN (SELECT player FROM attempts);", ("$c", before));
            using SqliteDataReader r = cmd.ExecuteReader();
            int runs = r.Read() ? r.GetInt32(0) : 0;
            return runs;
        }
    }

    // ---------------- Backups ----------------

    /// <summary>A consistent copy of the whole database, made while it's in use.</summary>
    public void Backup(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        Run("VACUUM INTO $path", ("$path", path));
    }
}
