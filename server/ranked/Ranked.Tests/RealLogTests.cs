using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Ranked;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Logs the game really wrote (RankedBotTests saves them in Temp/RankedLogs; without them these pass
/// with nothing to do), on the server's runtime. Honest ones must pass, with the same score. Then a
/// careful cheater changes each and tidies up after the change, as <see cref="Tidy"/> does: only the
/// cheat itself can give the log away.
/// </summary>
public sealed class RealLogTests
{
    private readonly ITestOutputHelper _out;

    public RealLogTests(ITestOutputHelper output) => _out = output;

    private static string Repo()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Demolition Men")))
            dir = Path.GetDirectoryName(dir);
        return dir;
    }

    private static (string Name, byte[] Bytes, LevelLayout Layout)[] Logs()
    {
        string folder = Path.Combine(Repo(), "Demolition Men", "Temp", "RankedLogs");
        if (!Directory.Exists(folder))
            return [];
        var levels = new Levels(Path.Combine(Repo(), "Demolition Men", "Assets", "Content"));
        return Directory.GetFiles(folder, "*.log").OrderBy(f => f).Select(f =>
        {
            byte[] bytes = File.ReadAllBytes(f);
            RunLog log = RunLog.Decode(bytes);
            LevelLayout layout = levels.Layout(new RankedDay { Day = log.Day, Template = log.Template, Seed = log.Seed, Mode = log.Mode });
            return (Path.GetFileNameWithoutExtension(f), bytes, layout);
        }).ToArray();
    }

    [Fact]
    public void RealLogs_PassTheReplay_WithTheScoreTheGameCounted()
    {
        foreach ((string name, byte[] bytes, LevelLayout layout) in Logs())
        {
            RunLog log = RunLog.Decode(bytes);
            Verdict verdict = RunVerifier.Verify(log, layout);
            _out.WriteLine($"{name}: {(verdict.Ok ? "ok" : verdict.Reason + " (event " + verdict.Event + ")")}, score {verdict.Score} of {log.Score}, finished {verdict.Finished}, {log.Events.Count} events");
            Assert.True(verdict.Ok, $"{name}: {verdict.Reason} (event {verdict.Event})");
            if (verdict.Finished)
                Assert.InRange(verdict.Score, log.Score - 1, log.Score + 1);
        }
    }

    // What a cheater fixes after changing a log: what follows from the change, not the change.
    private static bool Tidy(RunLog log, Verdict verdict)
    {
        string why = verdict.Reason;
        if (why.StartsWith("score:", StringComparison.Ordinal))
            log.Score = verdict.Score;
        else if (verdict.Event < 0)
            return false;
        else if (why.StartsWith("a block broke in the replay", StringComparison.Ordinal))
            log.Events[verdict.Event].Broke = true;
        else if (why.StartsWith("a block broke on the device", StringComparison.Ordinal))
            log.Events[verdict.Event].Broke = false;
        else if (why.StartsWith("damage to a block that isn't standing", StringComparison.Ordinal)
                 || why.StartsWith("crumble on a block", StringComparison.Ordinal)
                 || why.StartsWith("crumbling too fast", StringComparison.Ordinal)
                 || why.EndsWith("damage with no blast to match", StringComparison.Ordinal))
            log.Events.RemoveAt(verdict.Event);
        else
            return false;
        return true;
    }

    private static Verdict Judge(RunLog log, LevelLayout layout, bool tidy)
    {
        for (int tries = log.Events.Count + 100; ; tries--)
        {
            Verdict verdict = RunVerifier.Verify(RunLog.Decode(log.Encode()), layout);
            if (verdict.Ok || !tidy || tries <= 0 || !Tidy(log, verdict))
                return verdict;
        }
    }

    private static void Sort(RunLog log)
    {
        List<LogEvent> sorted = log.Events.OrderBy(e => e.TimeMs).ToList(); // stable
        log.Events.Clear();
        log.Events.AddRange(sorted);
    }

    private static LogEvent Copy(LogEvent e) => new LogEvent
    {
        TimeMs = e.TimeMs, Kind = e.Kind, Player = e.Player, Building = e.Building, CellX = e.CellX, CellY = e.CellY, Amount = e.Amount,
        Cause = e.Cause, Broke = e.Broke, Index = e.Index, X = e.X, Y = e.Y, Facing = e.Facing, Action = e.Action, Rejoins = e.Rejoins, Picks = e.Picks,
    };

    // Each damage of a cause lands this many times, this many ms apart.
    private static bool Repeat(RunLog log, HitCause cause, int times, int apart)
    {
        List<LogEvent> hits = log.Events.Where(e => e.Kind == LogKind.Damage && e.Cause == cause).ToList();
        foreach (LogEvent hit in hits)
            for (int i = 1; i < times; i++)
            {
                LogEvent again = Copy(hit);
                again.TimeMs += i * apart;
                log.Events.Add(again);
            }
        Sort(log);
        return hits.Count > 0;
    }

    private static bool Change(RunLog log, Func<LogEvent, bool> which, Action<LogEvent> change)
    {
        bool any = false;
        foreach (LogEvent e in log.Events.Where(which))
        {
            change(e);
            any = true;
        }
        return any;
    }

    private static bool Fist(LogEvent e) => e.Kind == LogKind.Damage && e.Cause == HitCause.Fist;

    // In the longest stretch with nothing happening, the track goes up or down and back: nothing
    // else to give it away.
    private static bool Climb(RunLog log, Func<int, float> height)
    {
        List<LogPose> poses = log.Poses[0];
        int from = 0, longest = 0, last = 0;
        foreach (LogEvent e in log.Events)
        {
            int sample = (int)(e.TimeMs / (RunLog.PoseInterval * 1000f));
            if (sample - last > longest)
            {
                longest = sample - last;
                from = last + 1;
            }
            last = sample;
        }
        longest -= 2;
        if (longest < 20)
            return false;
        for (int k = 0; k < longest && from + k < poses.Count; k++)
        {
            LogPose p = poses[from + k];
            poses[from + k] = new LogPose(p.X, p.Y + height(k), p.Left);
        }
        return true;
    }

    // The player waits this long, where the camera doesn't wait for anyone (up to the last screen's
    // worth of the level it stops at), and everything after happens later: the camera has gone on.
    private static bool Dawdle(RunLog log, LevelLayout level, float seconds, float share)
    {
        int ms = (int)(seconds * 1000f);
        float pushed = RankedRules.CameraStartLeft + RankedRules.WidestView * 2f * RankedRules.PushLine + 2f;
        float stop = level.FinishX - RankedRules.WidestView * 2f * RankedRules.PushLine - RankedRules.CameraMargin - RankedRules.CameraCoopMargin - 2f;
        if (stop <= pushed)
            return false; // the whole level is within one wide screen
        float x = pushed + (stop - pushed) * share;
        int sample = log.Poses[0].FindIndex(p => p.X >= x);
        if (sample < 0 || sample * 100 >= log.TimeMs || log.Poses[0][sample].X >= stop)
            return false;
        int at = sample * 100;
        foreach (LogEvent e in log.Events.Where(e => e.TimeMs > at))
            e.TimeMs += ms;
        log.TimeMs += ms;
        int extra = (int)(seconds / RunLog.PoseInterval);
        foreach (List<LogPose> poses in log.Poses)
            poses.InsertRange(Math.Min(sample, poses.Count), Enumerable.Repeat(poses[Math.Min(sample, poses.Count - 1)], extra));
        return true;
    }

    private static readonly (string Name, bool Tidy, Func<RunLog, LevelLayout, bool> Apply)[] Cheats =
    [
        ("a made-up score", false, (l, level) => { l.Score += 5000; return l.Events[^1].Kind == LogKind.Finish; }),
        ("a better time on the board", false, (l, level) => { l.TimeMs = Math.Max(0, l.TimeMs - 5000); return l.Events[^1].Kind == LogKind.Finish; }),
        ("another level", false, (l, level) => { l.LayoutHash ^= 1; return true; }),
        ("another game version", false, (l, level) => { l.Rules++; return true; }),
        ("punches three times as hard", true, (l, level) => Change(l, Fist, e => e.Amount *= 3f)),
        ("punches from afar", true, (l, level) => Change(l, Fist, e => e.X += 30f)),
        ("punches from where the player isn't", true, (l, level) => Change(l, Fist, e => e.Y += 8f)),
        ("five hits per punch", true, (l, level) => Repeat(l, HitCause.Fist, 5, 0)),
        ("five hits per axe swing", true, (l, level) => Repeat(l, HitCause.Axe, 5, 0)),
        ("five hits per ball swing", true, (l, level) => Repeat(l, HitCause.WreckingBall, 5, 0)),
        ("punches four times as fast", true, (l, level) => Repeat(l, HitCause.Fist, 4, 50)),
        ("axe swings twice as fast", true, (l, level) => Repeat(l, HitCause.Axe, 2, 60)),
        ("headbutts ten times as fast", true, (l, level) => Repeat(l, HitCause.Headbutt, 3, 30)),
        ("three times the damage per blast", true, (l, level) => Repeat(l, HitCause.Dynamite, 3, 0)),
        ("a gas tank's blast thrice", true, (l, level) => Repeat(l, HitCause.GasTank, 3, 0)),
        ("the crumble ticks thrice", true, (l, level) => Repeat(l, HitCause.Crumble, 3, 0)),
        ("the crumble ten times as fast", true, (l, level) => Repeat(l, HitCause.Crumble, 10, 10)),
        ("the intern hits five times per punch", true, (l, level) => Repeat(l, HitCause.Intern, 5, 0)),
        ("the intern punches five times as fast", true, (l, level) => Repeat(l, HitCause.Intern, 5, 30)),
        ("the slam's ring thrice", true, (l, level) => Repeat(l, HitCause.ButtSlam, 3, 0)),
        ("the radio ten times as loud", true, (l, level) => Change(l, e => e.Kind == LogKind.Damage && e.Cause == HitCause.Radio, e => e.Amount *= 10f)),
        ("a weapon never picked up", true, (l, level) => Change(l, Fist, e => e.Cause = HitCause.Axe)),
        ("a burp never burped", true, (l, level) => Change(l, Fist, e => e.Cause = HitCause.Burp)),
        ("a stick never picked up", true, (l, level) =>
        {
            l.Events.Insert(0, new LogEvent { TimeMs = 0, Kind = LogKind.Throw, Player = 0, X = l.Poses[0][0].X + 1f, Y = l.Poses[0][0].Y + 1.3f });
            return !l.Events.Any(e => e.Kind == LogKind.Pickup);
        }),
        ("a second stick while one is out", true, (l, level) =>
        {
            List<LogEvent> throws = l.Events.Where(e => e.Kind == LogKind.Throw).ToList();
            foreach (LogEvent t in throws)
            {
                LogEvent again = Copy(t);
                again.TimeMs += 20;
                l.Events.Add(again);
            }
            Sort(l);
            return throws.Count > 0;
        }),
        ("an action on an empty meter", true, (l, level) =>
        {
            l.Events.Insert(0, new LogEvent { TimeMs = 0, Kind = LogKind.ActionStart, Player = 0, Action = l.Actions[0] });
            return true;
        }),
        ("a strike with no action", true, (l, level) =>
        {
            l.Events.Insert(0, new LogEvent { TimeMs = 0, Kind = LogKind.ActionStrike, Player = 0, Action = l.Actions[0], X = l.Poses[0][0].X, Y = l.Poses[0][0].Y });
            return true;
        }),
        ("a teleport", true, (l, level) =>
        {
            List<LogPose> poses = l.Poses[0];
            LogPose p = poses[poses.Count / 2];
            poses[poses.Count / 2] = new LogPose(p.X + 25f, p.Y, p.Left);
            return true;
        }),
        ("a straight climb to the sky", true, (l, level) => Climb(l, k => k * 1.2f)),
        ("a climb with a rest in every jump", true, (l, level) => Climb(l, k => (k - k / 8) * 2f)),
        ("standing below the level", true, (l, level) => Climb(l, k => -40f)),
        ("waiting behind the camera", true, (l, level) => Dawdle(l, level, 180f, 0.25f)),
        ("going back for the rest", true, (l, level) => Dawdle(l, level, 90f, 0.75f)),
        ("out of play and never paying", true, (l, level) => l.Events.RemoveAll(e => e.Kind == LogKind.Out || e.Kind == LogKind.Rejoin) > 0),
        ("finishing a lost run", true, (l, level) => Change(l, e => e.Kind == LogKind.Fail, e => e.Kind = LogKind.Finish)),
        ("twice as fast all the way", true, (l, level) =>
        {
            foreach (LogEvent e in l.Events)
                e.TimeMs /= 2;
            l.TimeMs /= 2;
            for (int p = 0; p < l.Poses.Length; p++)
                l.Poses[p] = l.Poses[p].Where((_, i) => i % 2 == 0).ToList();
            return true;
        }),
    ];

    [Fact]
    public void EveryCheat_OnARealLog_IsTurnedDown()
    {
        var through = new List<string>();
        (string Name, byte[] Bytes, LevelLayout Layout)[] logs = Logs();
        foreach ((string name, bool tidy, Func<RunLog, LevelLayout, bool> apply) in Cheats)
        {
            int applied = 0;
            var reasons = new Dictionary<string, int>();
            foreach ((string log, byte[] bytes, LevelLayout layout) in logs)
            {
                RunLog changed = RunLog.Decode(bytes);
                if (!apply(changed, layout))
                    continue;
                applied++;
                Verdict verdict = Judge(changed, layout, tidy);
                // Tidying can leave nothing of the cheat (every extra hit on a block already gone), and
                // only a finished run goes on a board.
                bool undone = changed.Encode().SequenceEqual(bytes);
                if (verdict.Ok && verdict.Finished && !undone)
                    through.Add($"{name} on {log}: score {verdict.Score}");
                string why = verdict.Ok ? (undone ? "nothing left of it" : verdict.Finished ? "PASSED" : "lost run, no board") : new string(verdict.Reason.TakeWhile(c => !char.IsDigit(c)).ToArray()).Trim();
                reasons[why] = reasons.GetValueOrDefault(why) + 1;
            }
            _out.WriteLine($"{name}: on {applied} logs: " + string.Join("; ", reasons.OrderByDescending(r => r.Value).Select(r => $"{r.Value}× {r.Key}")));
        }
        Assert.True(through.Count == 0, "Through the replay:\n" + string.Join("\n", through));
    }
}
