using System;
using System.Collections.Generic;
using System.Text;
using Demolition.Actions;

namespace Demolition.Ranked
{
    public enum RankedMode : byte { Solo, Coop }

    /// <summary>What took health off a standing block. Every path that damages one names its cause,
    /// so the server can hold the damage to that cause's rules (docs/ranked.md §4).</summary>
    public enum HitCause : byte
    {
        Unknown, Fist, Axe, WreckingBall, Headbutt, Glass, Dynamite, GasTank, Charge,
        Burp, ButtSlam, Crane, Intern, Radio, Crumble,
    }

    public enum LogKind : byte { Damage, Pickup, Throw, Blast, ActionStart, ActionStrike, Out, Rejoin, Finish, Fail }

    /// <summary>Where a player's feet are, relative to the level's origin.</summary>
    public struct LogPose
    {
        public float X, Y;
        public bool Left;

        public LogPose(float x, float y, bool left)
        {
            X = x;
            Y = y;
            Left = left;
        }
    }

    /// <summary>One thing that happened in a run. Which fields count depends on <see cref="Kind"/>.</summary>
    public sealed class LogEvent
    {
        /// <summary>Milliseconds since GO.</summary>
        public int TimeMs;
        public LogKind Kind;
        /// <summary>The player it's about (the damage's instigator), -1 for none.</summary>
        public int Player = -1;
        /// <summary>Damage: the building's index in the layout, and a cell of the block (the building's own grid).</summary>
        public int Building, CellX, CellY;
        /// <summary>Damage: the health the device took off, exactly.</summary>
        public float Amount;
        /// <summary>Damage and blasts: what did it.</summary>
        public HitCause Cause;
        /// <summary>Damage: the block broke.</summary>
        public bool Broke;
        /// <summary>Pickup: its index in the layout.</summary>
        public int Index;
        /// <summary>Where it came from: the swing's centre, the throw, the blast, the strike, the rejoin.</summary>
        public float X, Y;
        /// <summary>Action strike: the way the player faced.</summary>
        public int Facing = 1;
        public ActionKind Action;
        /// <summary>Out: the player comes back (and pays the rejoin penalty).</summary>
        public bool Rejoins;
        /// <summary>Action strike: the spots the strike picked (x, y pairs), or null.</summary>
        public float[] Picks;

        public float Time => TimeMs / 1000f;
    }

    /// <summary>
    /// The log of a ranked run (docs/ranked.md §3): who played what, where each player was 10
    /// times a second, and every event that can change the score. Bits, not text. The server reads
    /// logs from anyone, so <see cref="Decode"/> trusts nothing: anything out of bounds is a
    /// <see cref="FormatException"/>.
    /// </summary>
    public sealed class RunLog
    {
        public const int Format = 1;
        public const float PoseInterval = 0.1f;
        /// <summary>Positions are kept to 1/32 unit.</summary>
        public const int Quant = 32;
        public const int MaxPlayers = 2, MaxPicks = 8, MaxTemplate = 40;
        /// <summary>Half an hour of run: far past any level.</summary>
        public const int MaxTimeMs = 30 * 60 * 1000;
        public const int MaxSamples = MaxTimeMs / 100 + 1, MaxEvents = 60000;
        /// <summary>No position is further than this from the level's origin (in 1/32 units).</summary>
        private const long MaxCoord = 100000L * Quant;
        private static readonly byte[] Magic = { (byte)'D', (byte)'M', (byte)'R' };

        public int Rules;
        public RankedMode Mode;
        public bool Online;
        /// <summary>The ranked day as yyyymmdd (UTC).</summary>
        public int Day;
        /// <summary>The campaign level the day's level is built from, and its seed.</summary>
        public string Template = string.Empty;
        public int Seed;
        public int LayoutHash;
        /// <summary>The action each player carries; its length is the number of players.</summary>
        public ActionKind[] Actions = { ActionCatalog.Default };
        /// <summary>Per player, one pose every <see cref="PoseInterval"/> from GO.</summary>
        public List<LogPose>[] Poses = { new List<LogPose>() };
        public readonly List<LogEvent> Events = new List<LogEvent>();
        /// <summary>The device's own result, only to tell where a replay went another way.</summary>
        public int Score;
        public int TimeMs;

        public int Players => Actions.Length;

        public static RunLog For(int players)
        {
            players = Math.Max(1, Math.Min(MaxPlayers, players));
            var log = new RunLog { Actions = new ActionKind[players], Poses = new List<LogPose>[players] };
            for (int i = 0; i < players; i++)
            {
                log.Actions[i] = ActionCatalog.Default;
                log.Poses[i] = new List<LogPose>();
            }
            return log;
        }

        // ---------------- Writing ----------------

        public byte[] Encode()
        {
            var w = new Writer();
            w.Bytes(Magic);
            w.Byte(Format);
            w.Uint(Rules);
            w.Byte((byte)Mode);
            w.Byte((byte)(Online ? 1 : 0));
            w.Uint(Day);
            byte[] template = Encoding.ASCII.GetBytes(Template ?? string.Empty);
            w.Uint(template.Length);
            w.Bytes(template);
            w.Int(Seed);
            w.Int(LayoutHash);
            w.Byte((byte)Players);
            for (int p = 0; p < Players; p++)
                w.Byte((byte)Actions[p]);
            for (int p = 0; p < Players; p++)
            {
                List<LogPose> poses = Poses[p];
                w.Uint(poses.Count);
                int lastX = 0, lastY = 0;
                foreach (LogPose pose in poses)
                {
                    int x = Q(pose.X), y = Q(pose.Y);
                    w.Int(((x - lastX) << 1) | (pose.Left ? 1 : 0));
                    w.Int(y - lastY);
                    lastX = x;
                    lastY = y;
                }
            }
            w.Uint(Events.Count);
            int lastTime = 0;
            foreach (LogEvent e in Events)
            {
                w.Uint(e.TimeMs - lastTime); // a negative step throws: the recorder writes in order
                lastTime = e.TimeMs;
                w.Byte((byte)e.Kind);
                w.Byte((byte)(e.Player + 1));
                switch (e.Kind)
                {
                    case LogKind.Damage:
                        w.Uint(e.Building);
                        w.Int(e.CellX);
                        w.Int(e.CellY);
                        w.Float(e.Amount);
                        w.Byte((byte)e.Cause);
                        w.Byte((byte)(e.Broke ? 1 : 0));
                        w.Point(e.X, e.Y);
                        break;
                    case LogKind.Pickup:
                        w.Uint(e.Index);
                        break;
                    case LogKind.Throw:
                    case LogKind.Rejoin:
                        w.Point(e.X, e.Y);
                        break;
                    case LogKind.Blast:
                        w.Byte((byte)e.Cause);
                        w.Point(e.X, e.Y);
                        break;
                    case LogKind.ActionStart:
                        w.Byte((byte)e.Action);
                        break;
                    case LogKind.ActionStrike:
                        w.Byte((byte)e.Action);
                        w.Byte((byte)(e.Facing < 0 ? 1 : 0));
                        w.Point(e.X, e.Y);
                        int picks = e.Picks == null ? 0 : e.Picks.Length / 2;
                        w.Byte((byte)picks);
                        for (int i = 0; i < picks; i++)
                            w.Point(e.Picks[i * 2], e.Picks[i * 2 + 1]);
                        break;
                    case LogKind.Out:
                        w.Byte((byte)(e.Rejoins ? 1 : 0));
                        break;
                }
            }
            w.Uint(Score);
            w.Uint(TimeMs);
            return w.ToArray();
        }

        // ---------------- Reading ----------------

        /// <exception cref="FormatException">Not a log this build writes, or out of bounds.</exception>
        public static RunLog Decode(byte[] bytes)
        {
            var r = new Reader(bytes);
            foreach (byte b in Magic)
                r.Expect(r.Byte() == b, "not a run log");
            r.Expect(r.Byte() == Format, "another log format");
            int rules = r.Uint(int.MaxValue);
            var mode = (RankedMode)r.Byte();
            r.Expect(mode == RankedMode.Solo || mode == RankedMode.Coop, "mode");
            int flags = r.Byte();
            int day = r.Uint(99991231);
            byte[] template = r.Bytes(r.Uint(MaxTemplate));
            foreach (byte b in template)
                r.Expect(b > 32 && b < 127, "template");
            int seed = r.Int();
            int hash = r.Int();
            int players = r.Byte();
            r.Expect(players >= 1 && players <= MaxPlayers, "players");
            r.Expect((mode == RankedMode.Solo) == (players == 1), "players for the mode");
            RunLog log = For(players);
            log.Rules = rules;
            log.Mode = mode;
            log.Online = (flags & 1) != 0;
            log.Day = day;
            log.Template = Encoding.ASCII.GetString(template);
            log.Seed = seed;
            log.LayoutHash = hash;
            for (int p = 0; p < players; p++)
                log.Actions[p] = r.Action();
            for (int p = 0; p < players; p++)
            {
                int count = r.Uint(MaxSamples);
                long x = 0, y = 0;
                for (int i = 0; i < count; i++)
                {
                    int dx = r.Int();
                    x += dx >> 1;
                    y += r.Int();
                    r.Expect(Math.Abs(x) <= MaxCoord && Math.Abs(y) <= MaxCoord, "pose");
                    log.Poses[p].Add(new LogPose(x / (float)Quant, y / (float)Quant, (dx & 1) != 0));
                }
            }
            int events = r.Uint(MaxEvents);
            int time = 0;
            for (int i = 0; i < events; i++)
            {
                time += r.Uint(MaxTimeMs);
                r.Expect(time <= MaxTimeMs, "time");
                var e = new LogEvent { TimeMs = time, Kind = (LogKind)r.Byte(), Player = r.Byte() - 1 };
                r.Expect(e.Kind <= LogKind.Fail, "event kind");
                r.Expect(e.Player >= -1 && e.Player < players, "event player");
                switch (e.Kind)
                {
                    case LogKind.Damage:
                        e.Building = r.Uint(ushort.MaxValue);
                        e.CellX = r.Int();
                        e.CellY = r.Int();
                        e.Amount = r.Float();
                        r.Expect(e.Amount > 0f && e.Amount < 1e6f, "damage"); // NaN fails too
                        e.Cause = (HitCause)r.Byte();
                        r.Expect(e.Cause <= HitCause.Crumble, "cause");
                        e.Broke = r.Byte() != 0;
                        r.Point(out e.X, out e.Y);
                        break;
                    case LogKind.Pickup:
                        e.Index = r.Uint(ushort.MaxValue);
                        break;
                    case LogKind.Throw:
                    case LogKind.Rejoin:
                        r.Point(out e.X, out e.Y);
                        break;
                    case LogKind.Blast:
                        e.Cause = (HitCause)r.Byte();
                        r.Expect(e.Cause == HitCause.Dynamite || e.Cause == HitCause.GasTank || e.Cause == HitCause.Charge, "blast");
                        r.Point(out e.X, out e.Y);
                        break;
                    case LogKind.ActionStart:
                        e.Action = r.Action();
                        break;
                    case LogKind.ActionStrike:
                        e.Action = r.Action();
                        e.Facing = r.Byte() != 0 ? -1 : 1;
                        r.Point(out e.X, out e.Y);
                        int picks = r.Byte();
                        r.Expect(picks <= MaxPicks, "picks");
                        if (picks > 0)
                        {
                            e.Picks = new float[picks * 2];
                            for (int k = 0; k < picks; k++)
                                r.Point(out e.Picks[k * 2], out e.Picks[k * 2 + 1]);
                        }
                        break;
                    case LogKind.Out:
                        e.Rejoins = r.Byte() != 0;
                        break;
                }
                log.Events.Add(e);
            }
            log.Score = r.Uint(int.MaxValue);
            log.TimeMs = r.Uint(MaxTimeMs);
            r.Expect(r.AtEnd, "trailing bytes");
            return log;
        }

        private static int Q(float v) => (int)Math.Round(v * Quant);

        // Little-endian, unsigned LEB128 varints, signed ones zigzagged.
        private sealed class Writer
        {
            private readonly List<byte> _bytes = new List<byte>(4096);

            public byte[] ToArray() => _bytes.ToArray();
            public void Byte(int b) => _bytes.Add((byte)b);
            public void Bytes(byte[] b) => _bytes.AddRange(b);

            public void Uint(int value)
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                var v = (uint)value;
                while (v >= 0x80)
                {
                    _bytes.Add((byte)(v | 0x80));
                    v >>= 7;
                }
                _bytes.Add((byte)v);
            }

            public void Int(int value)
            {
                uint zig = (uint)((value << 1) ^ (value >> 31));
                while (zig >= 0x80)
                {
                    _bytes.Add((byte)(zig | 0x80));
                    zig >>= 7;
                }
                _bytes.Add((byte)zig);
            }

            public void Float(float value)
            {
                int bits = BitConverter.SingleToInt32Bits(value);
                for (int i = 0; i < 4; i++)
                    _bytes.Add((byte)(bits >> (i * 8)));
            }

            public void Point(float x, float y)
            {
                Int(Q(x));
                Int(Q(y));
            }
        }

        private sealed class Reader
        {
            private readonly byte[] _bytes;
            private int _at;

            public Reader(byte[] bytes) => _bytes = bytes ?? throw new FormatException("no log");

            public bool AtEnd => _at == _bytes.Length;

            public void Expect(bool ok, string what)
            {
                if (!ok)
                    throw new FormatException("bad run log: " + what);
            }

            public int Byte()
            {
                Expect(_at < _bytes.Length, "truncated");
                return _bytes[_at++];
            }

            public byte[] Bytes(int count)
            {
                Expect(count <= _bytes.Length - _at, "truncated");
                var b = new byte[count];
                Array.Copy(_bytes, _at, b, 0, count);
                _at += count;
                return b;
            }

            private uint Raw()
            {
                uint v = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    int b = Byte();
                    v |= (uint)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                    {
                        Expect(shift < 28 || b < 16, "varint"); // fits in 32 bits
                        return v;
                    }
                }
                throw new FormatException("bad run log: varint");
            }

            public int Uint(int max)
            {
                uint v = Raw();
                Expect(v <= (uint)max, "out of range");
                return (int)v;
            }

            public int Int()
            {
                uint zig = Raw();
                return (int)(zig >> 1) ^ -(int)(zig & 1);
            }

            public float Float()
            {
                int bits = 0;
                for (int i = 0; i < 4; i++)
                    bits |= Byte() << (i * 8);
                return BitConverter.Int32BitsToSingle(bits);
            }

            public void Point(out float x, out float y)
            {
                x = Int() / (float)Quant;
                y = Int() / (float)Quant;
            }

            public ActionKind Action()
            {
                int kind = Byte();
                Expect(kind <= (int)ActionKind.SiteRadio, "action");
                return (ActionKind)kind;
            }
        }
    }
}
