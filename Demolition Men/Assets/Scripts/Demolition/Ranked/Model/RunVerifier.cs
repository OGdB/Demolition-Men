using System;
using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Core;
using Demolition.Generation;
using Demolition.Levels;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Ranked
{
    /// <summary>The server's word on a ranked run.</summary>
    public sealed class Verdict
    {
        /// <summary>The rules allow everything in the log.</summary>
        public bool Ok;
        /// <summary>Why not: for the server's records, never shown to the player.</summary>
        public string Reason = string.Empty;
        /// <summary>The event the replay stopped at, -1 for none (a header or the pose track).</summary>
        public int Event = -1;
        /// <summary>The run reached the finish: only these go on a board.</summary>
        public bool Finished;
        /// <summary>The score as the replay counts it, speed bonus included.</summary>
        public int Score;
        public int TimeMs;
        /// <summary>Share of the level's cells brought down (the stars).</summary>
        public float Destroyed;
    }

    /// <summary>
    /// The referee of a ranked run (docs/ranked.md §4). Rebuilds the level, replays the log's
    /// events in order, takes each damage off its own copy of the block's health, lets
    /// <see cref="BuildingStructure"/> decide what detaches, scores it with <see cref="RunScore"/>,
    /// and holds every claim to the rules of its cause (<see cref="RankedRules"/>). Where the
    /// device's timing can't be known exactly (a pose between samples, an action meter between
    /// frames, the stress on another platform) the server's estimate leans the device's way, so an
    /// honest run never fails; what's left out of reach is what the rules don't allow.
    /// </summary>
    public sealed class RunVerifier
    {
        public static Verdict Verify(RunLog log, LevelLayout layout) => new RunVerifier(log, layout).Run();

        private sealed class Site
        {
            public BuildingPlacement Placement;
            public BuildingStructure Structure;
            public Dictionary<Vector2Int, CellStress> Baseline;
            public readonly Dictionary<Vector2Int, Block> Cells = new Dictionary<Vector2Int, Block>();
            /// <summary>Standing blocks overloaded enough to crumble, by their worst cell's overload past baseline.</summary>
            public readonly Dictionary<Block, float> Crumbling = new Dictionary<Block, float>();
            public Dictionary<Block, float> TickCrumbling = new Dictionary<Block, float>();
            public int LastInstigator = -1;
            public float CrumbleSince = -1f, TickAt = -1f, TickElapsed;
        }

        private sealed class Block
        {
            public SpecBlock Spec;
            public Site Site;
            public float Health;
            public bool Standing = true, Credited, Explosive;
            public int Instigator = -1;
            public float X0, Y0, X1, Y1;
            public float LastRattle = float.NegativeInfinity;
        }

        private sealed class Crew
        {
            public ActionInfo Action;
            public float Charge, Idle;
            public bool Starting;
            public float StartedAt, BusyUntil = float.NegativeInfinity;
            public readonly List<LogEvent> Strikes = new List<LogEvent>();
            public readonly Dictionary<LogEvent, HashSet<int>> CraneImpacts = new Dictionary<LogEvent, HashSet<int>>();
            public int Dynamite;
            public LogEvent Thrown;
            public int ToolUses, BallUses, ToolSwings, BallSwings;
            public int LastSwingMs = int.MinValue;
            public HitCause LastSwingCause;
            public readonly List<float> FistSwings = new List<float>(), ToolSwingTimes = new List<float>();
            public float LastHeadbutt = float.NegativeInfinity;
            public float CoffeeUntil = float.NegativeInfinity, BoostUntil = float.NegativeInfinity;
            public readonly List<float> CoffeeFrom = new List<float>();
            public bool Out;
            public float OutAt;
            /// <summary>Out of play: from, to (+inf when never back).</summary>
            public readonly List<Vector2> OutSpans = new List<Vector2>();
        }

        private struct Knock
        {
            public float From, To, X, Y;
        }

        private struct Barrel
        {
            public Block Block;
            public float Time;
            public bool Used;
        }

        private const float Epsilon = 1.0001f;
        /// <summary>Hits counted in a sliding window this long (frames and network jitter even out).</summary>
        private const float RateWindow = 2f;

        private readonly RunLog _log;
        private readonly LevelLayout _layout;
        private readonly Verdict _verdict = new Verdict();
        private readonly List<Site> _sites = new List<Site>();
        private readonly Crew[] _crew;
        private readonly RunScore _score;
        private readonly bool[] _taken;
        private readonly List<LogEvent> _blasts = new List<LogEvent>();
        private readonly List<Knock> _knocks = new List<Knock>();
        private readonly List<Barrel> _barrels = new List<Barrel>();
        private int _totalCells, _removedCells, _sampled;
        private float _now;
        private bool _ended;

        private RunVerifier(RunLog log, LevelLayout layout)
        {
            _log = log;
            _layout = layout;
            _crew = new Crew[log.Players];
            for (int i = 0; i < _crew.Length; i++)
                _crew[i] = new Crew { Action = ActionCatalog.Get(log.Actions[i]) };
            _score = new RunScore(log.Players);
            _taken = new bool[layout.Pickups.Count];
        }

        private Verdict Run()
        {
            if (_log.Rules != RankedRules.Version)
                return Fail("rules version " + _log.Rules);
            if (_log.LayoutHash != _layout.Hash())
                return Fail("another level (layout hash)");
            Build();
            foreach (LogEvent e in _log.Events)
                if (e.Kind == LogKind.Blast)
                    _blasts.Add(e);
            for (_verdict.Event = 0; _verdict.Event < _log.Events.Count; _verdict.Event++)
            {
                LogEvent e = _log.Events[_verdict.Event];
                if (_ended)
                    return Fail("events after the end");
                if (!AdvanceTo(e.Time) || !Handle(e))
                    return _verdict;
            }
            _verdict.Event = -1;
            if (!_ended)
                return Fail("no finish or fail");
            if (!Movement())
                return _verdict;
            _verdict.Destroyed = _totalCells > 0 ? _removedCells / (float)_totalCells : 0f;
            if (_verdict.Finished)
            {
                _verdict.Score = _score.FinalTotal(_verdict.TimeMs / 1000f, _layout.Par);
                if (_verdict.Score != _log.Score)
                    return Fail($"score: the device counted {_log.Score}, the replay {_verdict.Score}");
            }
            _verdict.Ok = true;
            return _verdict;
        }

        private Verdict Fail(string reason)
        {
            _verdict.Ok = false;
            _verdict.Reason = reason;
            return _verdict;
        }

        private bool No(string reason)
        {
            Fail(reason);
            return false;
        }

        // ---------------- The level ----------------

        private void Build()
        {
            foreach (BuildingPlacement placement in _layout.Buildings)
            {
                var site = new Site { Placement = placement, Structure = placement.Spec.ToStructure() };
                foreach (SpecBlock spec in placement.Spec.Blocks)
                {
                    float health = TileKinds.Health(spec.Kind);
                    var block = new Block
                    {
                        Spec = spec,
                        Site = site,
                        Health = health > 0f ? health : BlockMaterialInfo.DefaultHealth(spec.Material),
                        Explosive = TileKinds.Explosive(spec.Kind),
                        X0 = placement.X + spec.Rect.x,
                        Y0 = placement.GroundY + spec.Rect.y,
                    };
                    block.X1 = block.X0 + spec.Rect.width;
                    block.Y1 = block.Y0 + spec.Rect.height;
                    foreach (Vector2Int cell in spec.Cells())
                        site.Cells[cell] = block;
                }
                // As DestructibleBuilding.Start: what doesn't touch the rest falls at once, unscored.
                foreach (Vector2Int cell in site.Structure.RemoveUnsupported())
                    if (site.Cells.TryGetValue(cell, out Block loose))
                    {
                        site.Cells.Remove(cell);
                        loose.Standing = false;
                        loose.Credited = true;
                    }
                site.Baseline = Stress(site);
                _totalCells += site.Cells.Count;
                _sites.Add(site);
            }
        }

        private static Dictionary<Vector2Int, CellStress> Stress(Site site) => site.Structure.ComputeStress(
            StructureDefaults.StressScanRange, StructureDefaults.ColumnLoadCapacity, StructureDefaults.ColumnMomentCapacity);

        // As DestructibleBuilding.OnBlockDestroyed.
        private void Break(Block block, float time)
        {
            Site site = block.Site;
            block.Standing = false;
            int cells = Mathf.Max(1, block.Spec.Rect.width * block.Spec.Rect.height);
            if (!block.Credited)
            {
                block.Credited = true;
                _score.AddDestroyed(block.Instigator, block.Spec.Material, cells);
                Charge(block.Instigator, ActionMeter.PointsFor(cells, collapse: false));
            }
            int instigator = block.Instigator >= 0 ? block.Instigator : site.LastInstigator;
            site.LastInstigator = instigator;

            var coords = new List<Vector2Int>(block.Spec.Cells());
            int removed = 0;
            foreach (Vector2Int cell in coords)
                if (site.Cells.Remove(cell))
                    removed++;
            var collapsed = new List<BlockMaterial>();
            foreach (Vector2Int cell in site.Structure.RemoveCells(coords))
            {
                if (!site.Cells.TryGetValue(cell, out Block fallen))
                    continue;
                site.Cells.Remove(cell);
                removed++;
                collapsed.Add(fallen.Spec.Material);
                fallen.Standing = false;
                fallen.Credited = true;
            }
            _removedCells += removed;
            if (collapsed.Count > 0)
            {
                _score.AddCollapse(instigator, collapsed);
                Charge(instigator, ActionMeter.PointsFor(collapsed.Count, collapse: true));
            }
            if (block.Explosive)
                _barrels.Add(new Barrel { Block = block, Time = time });
            Restress(site, time);
        }

        // As DestructibleBuilding.RefreshStressVisuals: which standing blocks crumble now.
        private static void Restress(Site site, float time)
        {
            bool was = site.Crumbling.Count > 0;
            site.Crumbling.Clear();
            Dictionary<Vector2Int, CellStress> stress = Stress(site);
            foreach (KeyValuePair<Vector2Int, Block> kv in site.Cells)
            {
                float over = stress.TryGetValue(kv.Key, out CellStress cs) ? cs.Overload : 0f;
                if (site.Baseline.TryGetValue(kv.Key, out CellStress baseline))
                    over = Mathf.Max(0f, over - baseline.Overload);
                if (over <= StructureDefaults.OverloadTolerance - RankedRules.CrumbleMargin)
                    continue;
                if (!site.Crumbling.TryGetValue(kv.Value, out float worst) || over > worst)
                    site.Crumbling[kv.Value] = over;
            }
            if (site.Crumbling.Count == 0)
                site.CrumbleSince = -1f;
            else if (!was)
                site.CrumbleSince = time;
        }

        // ---------------- The timeline ----------------

        private bool AdvanceTo(float time)
        {
            foreach (Crew c in _crew)
                Drain(c, _now, time);
            // Every pose up to now against the blocks standing now: nobody walks through a wall.
            for (; _sampled * RunLog.PoseInterval <= time; _sampled++)
                for (int p = 0; p < _crew.Length; p++)
                {
                    if (_crew[p].Out || _sampled >= _log.Poses[p].Count)
                        continue;
                    Pose pose = _log.Poses[p][_sampled];
                    if (InsideWall(pose.X, pose.Y + 0.5f) || InsideWall(pose.X, pose.Y + 1.5f)) // the legs, the chest
                        return No($"player {p + 1} inside a wall at {_sampled * RunLog.PoseInterval:0.0}s");
                }
            _now = time;
            return true;
        }

        private bool InsideWall(float x, float y)
        {
            const float Inset = 0.3f;
            foreach (Site site in _sites)
            {
                BuildingPlacement b = site.Placement;
                if (x < b.X || x >= b.X + b.Spec.Width)
                    continue;
                var cell = new Vector2Int(Mathf.FloorToInt(x - b.X), Mathf.FloorToInt(y - b.GroundY));
                if (!site.Cells.TryGetValue(cell, out Block block) || TileKinds.WalkThrough(block.Spec.Kind)
                    || block.Spec.Material == BlockMaterial.Glass)
                    continue;
                if (x > block.X0 + Inset && x < block.X1 - Inset && y > block.Y0 + Inset && y < block.Y1 - Inset)
                    return true;
            }
            return false;
        }

        // ActionMeter.Tick, leaning high: no drain while an action plays, and none of a frame's
        // worth the device drains when its idle time runs out mid-frame.
        private static void Drain(Crew c, float from, float to)
        {
            if (c.Starting)
                return;
            from = Mathf.Max(from, c.BusyUntil);
            float dt = to - from;
            if (dt <= 0f || c.Charge >= c.Action.Charge)
                return;
            float over = Mathf.Max(0f, c.Idle + dt - ActionMeter.IdleGrace) - Mathf.Max(0f, c.Idle - ActionMeter.IdleGrace);
            c.Charge = Mathf.Max(0f, c.Charge - ActionMeter.DrainPerSecond * c.Action.Charge * over);
            c.Idle += dt;
        }

        // ActionMeter.Add, from everything the player breaks (the game leaves out what its own
        // action breaks; counting it too only leans high).
        private void Charge(int player, float points)
        {
            if (player < 0 || player >= _crew.Length || points <= 0f)
                return;
            Crew c = _crew[player];
            c.Charge = Mathf.Min(c.Action.Charge, c.Charge + points);
            c.Idle = 0f;
        }

        private bool Handle(LogEvent e)
        {
            switch (e.Kind)
            {
                case LogKind.Damage: return Damage(e);
                case LogKind.Pickup: return Pickup(e);
                case LogKind.Throw: return Throw(e);
                case LogKind.Blast: return Blast(e);
                case LogKind.ActionStart: return ActionStart(e);
                case LogKind.ActionStrike: return ActionStrike(e);
                case LogKind.Out: return Out(e);
                case LogKind.Rejoin: return Rejoin(e);
                case LogKind.Finish: return Finish(e);
                default:
                    _ended = true;
                    _verdict.TimeMs = e.TimeMs;
                    return true;
            }
        }

        private bool Who(LogEvent e, out Crew crew)
        {
            crew = e.Player >= 0 && e.Player < _crew.Length ? _crew[e.Player] : null;
            if (crew == null)
                return No("no player");
            if (crew.Out)
                return No($"player {e.Player + 1} acted while out of play");
            return true;
        }

        // ---------------- Damage ----------------

        private bool Damage(LogEvent e)
        {
            if (e.Building >= _sites.Count)
                return No("no such building");
            Site site = _sites[e.Building];
            if (!site.Cells.TryGetValue(new Vector2Int(e.CellX, e.CellY), out Block block) || !block.Standing)
                return No("damage to a block that isn't standing");
            if (e.Player >= 0 && (e.Player >= _crew.Length || _crew[e.Player].Out))
                return No("damage from a player out of play");
            if (!Allowed(e, block))
                return false;
            if (e.Player >= 0)
                block.Instigator = e.Player;
            block.Health -= e.Amount;
            bool broke = block.Health <= 0f;
            if (broke != e.Broke)
                return No(broke ? "a block broke in the replay, not on the device" : "a block broke on the device, not in the replay");
            if (broke)
                Break(block, e.Time);
            return true;
        }

        private bool Allowed(LogEvent e, Block block)
        {
            switch (e.Cause)
            {
                case HitCause.Fist:
                case HitCause.Axe:
                case HitCause.WreckingBall: return Swing(e, block);
                case HitCause.Headbutt: return Headbutt(e, block);
                case HitCause.Glass: return Touch(e, block);
                case HitCause.Dynamite:
                case HitCause.GasTank: return Blasted(e, block);
                case HitCause.Charge: return Charged(e, block);
                case HitCause.Crumble: return Crumbled(e, block);
                case HitCause.Burp:
                case HitCause.ButtSlam:
                case HitCause.Crane:
                case HitCause.Intern:
                case HitCause.Radio: return Struck(e, block);
                default: return No("damage with no cause");
            }
        }

        private bool Swing(LogEvent e, Block block)
        {
            if (!Who(e, out Crew c))
                return false;
            Pose pose = PoseAt(e.Player, e.Time);
            float slack = Slack(e.Player);
            if (Mathf.Abs(e.X - pose.X) > RankedRules.AttackOffsetX + slack || Mathf.Abs(e.Y - pose.Y - RankedRules.AttackOffsetY) > slack)
                return No("a swing out of the player's reach");
            if (Distance(e.X, e.Y, block) > RankedRules.AttackRadius + 0.05f)
                return No("a block outside the swing");
            float[] profile = e.Cause == HitCause.Fist ? RankedRules.Fist : e.Cause == HitCause.Axe ? RankedRules.Axe : RankedRules.WreckingBall;
            if (e.Amount > profile[(int)block.Spec.Material] * Buffs(c, e.Time) * Epsilon)
                return No($"a {e.Cause} hit too hard");
            if (e.TimeMs == c.LastSwingMs && e.Cause == c.LastSwingCause)
                return true; // the same swing, another block
            c.LastSwingMs = e.TimeMs;
            c.LastSwingCause = e.Cause;
            if (e.Cause == HitCause.Axe && ++c.ToolSwings > c.ToolUses)
                return No("an axe the player didn't have");
            if (e.Cause == HitCause.WreckingBall && ++c.BallSwings > c.BallUses)
                return No("a wrecking ball the player didn't have");
            bool fist = e.Cause == HitCause.Fist;
            List<float> swings = fist ? c.FistSwings : c.ToolSwingTimes;
            float interval = (fist ? RankedRules.FistInterval : RankedRules.ToolInterval) / (CoffeeIn(c, e.Time - RateWindow, e.Time) ? RankedRules.CoffeeSwing : 1f);
            // An online partner's hits arrive in bunches: only the window counts for them.
            if (swings.Count > 0 && e.Time - swings[swings.Count - 1] < interval * RankedRules.SwingSlack && !OnlinePartner(e.Player))
                return No($"swings too fast ({e.Time - swings[swings.Count - 1]:0.000}s apart)");
            swings.Add(e.Time);
            int inWindow = 0;
            for (int i = swings.Count - 1; i >= 0 && swings[i] > e.Time - RateWindow; i--)
                inWindow++;
            if (inWindow > Mathf.FloorToInt(RateWindow / interval) + 2)
                return No($"swings too fast ({inWindow} in {RateWindow}s)");
            return true;
        }

        private bool Headbutt(LogEvent e, Block block)
        {
            if (!Who(e, out Crew c))
                return false;
            Pose pose = PoseAt(e.Player, e.Time);
            if (block.Spec.Material == BlockMaterial.Glass
                || Distance(pose.X, pose.Y + RankedRules.HeadHeight + 0.6f, block) > 1f + Slack(e.Player))
                return No("a headbutt out of reach");
            if (e.Amount > RankedRules.Fist[(int)block.Spec.Material] * RankedRules.HeadbuttFactor * Buffs(c, e.Time) * Epsilon)
                return No("a headbutt too hard");
            if (e.Time - c.LastHeadbutt < 0.3f && !OnlinePartner(e.Player))
                return No("headbutts too fast");
            c.LastHeadbutt = e.Time;
            return true;
        }

        private bool Touch(LogEvent e, Block block)
        {
            if (!Who(e, out _))
                return false;
            Pose pose = PoseAt(e.Player, e.Time);
            if (block.Spec.Material != BlockMaterial.Glass || Distance(pose.X, pose.Y + 1.2f, block) > 1.6f + Slack(e.Player))
                return No("glass broken by a touch out of reach");
            return e.Amount <= Mathf.Max(block.Health, 1f) * Epsilon || No("glass touched too hard");
        }

        private bool Blasted(LogEvent e, Block block)
        {
            float window = e.Player >= 0 && OnlinePartner(e.Player) ? 1f : 0.002f;
            foreach (LogEvent blast in _blasts)
                if (blast.Cause == e.Cause && (e.Cause != HitCause.Dynamite || blast.Player == e.Player)
                    && Mathf.Abs(blast.Time - e.Time) <= window && Distance(blast.X, blast.Y, block) <= RankedRules.BlastRadius + 0.05f)
                    return e.Amount <= RankedRules.BlastDamage * Epsilon || No("a blast too hard");
            return No($"{e.Cause} damage with no blast to match");
        }

        private bool Charged(LogEvent e, Block block)
        {
            if (!Who(e, out Crew c))
                return false;
            foreach (LogEvent strike in c.Strikes)
            {
                if (strike.Action != ActionKind.Implosion || strike.Picks == null || e.Time < strike.Time
                    || e.Time > strike.Time + ChargesEnd(strike) + RankedRules.ActionSlack)
                    continue;
                for (int i = 0; i < strike.Picks.Length; i += 2)
                    if (Distance(strike.Picks[i], strike.Picks[i + 1], block) <= 0.1f)
                        return e.Amount <= RankedRules.Wreck * Epsilon || No("a charge too hard");
            }
            return Blasted(e, block);
        }

        private static float ChargesEnd(LogEvent strike) =>
            RankedRules.ChargeCountdown + RankedRules.ChargeRipple * (strike.Picks == null ? 0 : strike.Picks.Length / 2);

        private bool Struck(LogEvent e, Block block)
        {
            if (!Who(e, out Crew c))
                return false;
            ActionKind kind = e.Cause == HitCause.Burp ? ActionKind.Burp : e.Cause == HitCause.ButtSlam ? ActionKind.ButtSlam
                : e.Cause == HitCause.Crane ? ActionKind.CraneDrop : e.Cause == HitCause.Intern ? ActionKind.Intern : ActionKind.SiteRadio;
            float duration = ActionCatalog.Get(kind).Duration;
            for (int i = c.Strikes.Count - 1; i >= 0; i--)
            {
                LogEvent s = c.Strikes[i];
                if (s.Action != kind)
                    continue;
                float since = e.Time - s.Time;
                float slack = RankedRules.ActionSlack;
                switch (kind)
                {
                    case ActionKind.Burp:
                    {
                        if (since < 0f || since > RankedRules.BurpRange / RankedRules.BurpSpeed + slack)
                            continue;
                        float half = RankedRules.BurpHalfHeight + RankedRules.BurpSpread * RankedRules.BurpRange;
                        float x0 = Mathf.Min(s.X, s.X + s.Facing * RankedRules.BurpRange), x1 = Mathf.Max(s.X, s.X + s.Facing * RankedRules.BurpRange);
                        float y = s.Y - RankedRules.BurpDrop;
                        if (Overlaps(block, x0, y - half, x1, y + half))
                            return e.Amount <= RankedRules.Wreck * Epsilon || No("a burp too hard");
                        continue;
                    }
                    case ActionKind.ButtSlam:
                        if (since < 0f || since > slack)
                            continue;
                        if (Overlaps(block, s.X - RankedRules.SlamHalfWidth, s.Y - RankedRules.SlamDrop - RankedRules.SlamDepth,
                                s.X + RankedRules.SlamHalfWidth, s.Y - RankedRules.SlamDrop))
                            return e.Amount <= RankedRules.Wreck * Epsilon || No("a slam too hard");
                        if (Distance(s.X, s.Y + RankedRules.SlamRingRise, block) <= RankedRules.SlamRingRadius + 0.05f)
                            return e.Amount <= RankedRules.SlamRingDamage * Epsilon || No("a slam's ring too hard");
                        continue;
                    case ActionKind.CraneDrop:
                    {
                        // The pallet falls and bounces: anywhere near below the spot, at most three impacts.
                        if (s.Picks == null || since < RankedRules.CraneDelay - 0.2f || since > RankedRules.CraneDelay + 8f)
                            continue;
                        float gap = Mathf.Max(0f, Mathf.Max(block.X0 - s.Picks[0], s.Picks[0] - block.X1));
                        if (gap > RankedRules.CraneRadius + 3f || block.Y0 > s.Picks[1] + 3f)
                            continue;
                        if (!c.CraneImpacts.TryGetValue(s, out HashSet<int> impacts))
                            c.CraneImpacts[s] = impacts = new HashSet<int>();
                        impacts.Add(e.TimeMs);
                        if (impacts.Count > RankedRules.CraneImpacts)
                            return No("a pallet that kept landing");
                        return e.Amount <= RankedRules.Wreck * Epsilon || No("a pallet too hard");
                    }
                    case ActionKind.Intern:
                    {
                        if (since < 0f || since > duration + slack)
                            continue;
                        if (Mathf.Abs(e.X - s.X) > RankedRules.InternRunSpeed * since + 3f
                            || Distance(e.X, e.Y, block) > RankedRules.InternReach + 0.05f)
                            continue;
                        BlockMaterial m = block.Spec.Material;
                        float hit = m == BlockMaterial.Metal || m == BlockMaterial.Support ? RankedRules.InternMetalDamage : RankedRules.InternDamage;
                        return e.Amount <= hit * Buffs(c, s.Time) * Epsilon || No("an intern too strong");
                    }
                    default: // the radio
                    {
                        if (s.Picks == null || since < 0f || since > duration + slack
                            || Distance(s.Picks[0], s.Picks[1] + RankedRules.RadioRise, block) > RankedRules.RadioReach + 0.05f)
                            continue;
                        if (e.Time - block.LastRattle < RankedRules.RadioBeat * 0.8f)
                            return No("a radio beating too fast");
                        block.LastRattle = e.Time;
                        float max = block.Spec.Material == BlockMaterial.Glass ? Mathf.Max(block.Health, 1f) : RankedRules.RadioRattle;
                        return e.Amount <= max * Epsilon || No("a radio too loud");
                    }
                }
            }
            return No($"{e.Cause} damage with no strike to match");
        }

        private bool Crumbled(LogEvent e, Block block)
        {
            if (e.Player >= 0)
                return No("crumble with a player");
            Site site = block.Site;
            if (e.Time > site.TickAt)
            {
                // A new crumble tick: the device damages off the overload as it was when the tick began.
                float since = Mathf.Max(site.TickAt, site.CrumbleSince);
                site.TickElapsed = site.CrumbleSince < 0f ? 0f : e.Time - since;
                site.TickAt = e.Time;
                site.TickCrumbling = new Dictionary<Block, float>(site.Crumbling);
            }
            if (!site.TickCrumbling.TryGetValue(block, out float over))
                return No("crumble on a block that isn't overloaded");
            float excess = over - StructureDefaults.OverloadTolerance + RankedRules.CrumbleMargin;
            return e.Amount <= excess * StructureDefaults.CrumbleDamageRate * site.TickElapsed * 1.05f + 0.01f
                || No("crumbling too fast");
        }

        // ---------------- Pickups, dynamite, actions, players ----------------

        private bool Pickup(LogEvent e)
        {
            if (!Who(e, out Crew c))
                return false;
            if (e.Index >= _taken.Length || _taken[e.Index])
                return No("a pickup that isn't there");
            PickupPlacement pickup = _layout.Pickups[e.Index];
            if (pickup.Kind == PickupKind.Resurrect && _log.Mode != RankedMode.Coop)
                return No("a resurrect in solo");
            Pose pose = PoseAt(e.Player, e.Time);
            if (Vector2.Distance(new Vector2(pose.X, pose.Y + 1f), new Vector2(pickup.X, pickup.Y)) > RankedRules.PickupReach + Slack(e.Player))
                return No("a pickup out of reach");
            _taken[e.Index] = true;
            switch (pickup.Kind)
            {
                case PickupKind.Axe: c.ToolUses += pickup.Amount; break;
                case PickupKind.WreckingBall: c.BallUses += pickup.Amount; break;
                case PickupKind.Dynamite: c.Dynamite = Mathf.Min(RankedRules.MaxDynamite, c.Dynamite + pickup.Amount); break;
            }
            return true;
        }

        private bool Throw(LogEvent e)
        {
            if (!Who(e, out Crew c))
                return false;
            if (c.Dynamite <= 0)
                return No("dynamite the player didn't have");
            if (c.Thrown != null)
                return No("a second stick while one is out");
            Pose pose = PoseAt(e.Player, e.Time);
            float slack = Slack(e.Player);
            if (Mathf.Abs(e.X - pose.X) > RankedRules.ThrowOffsetX + slack || Mathf.Abs(e.Y - pose.Y - RankedRules.ThrowOffsetY) > slack)
                return No("a throw out of the player's hand");
            c.Dynamite--;
            c.Thrown = e;
            return true;
        }

        private bool Blast(LogEvent e)
        {
            switch (e.Cause)
            {
                case HitCause.Dynamite:
                {
                    if (!Who(e, out Crew c))
                        return false;
                    LogEvent thrown = c.Thrown;
                    if (thrown == null)
                        return No("a blast with no stick thrown");
                    float lag = OnlinePartner(e.Player) ? 1f : 0f;
                    if (e.Time - thrown.Time > RankedRules.Fuse + 0.5f + lag
                        || Vector2.Distance(new Vector2(e.X, e.Y), new Vector2(thrown.X, thrown.Y)) > 20f)
                        return No("a stick flew too far");
                    c.Thrown = null;
                    break;
                }
                case HitCause.GasTank:
                {
                    int found = -1;
                    for (int i = 0; i < _barrels.Count && found < 0; i++)
                    {
                        Barrel b = _barrels[i];
                        float since = e.Time - b.Time;
                        Vector2 centre = new Vector2((b.Block.X0 + b.Block.X1) * 0.5f, (b.Block.Y0 + b.Block.Y1) * 0.5f);
                        if (!b.Used && since >= -0.002f && since <= 0.35f && Vector2.Distance(centre, new Vector2(e.X, e.Y)) <= 0.75f)
                            found = i;
                    }
                    if (found < 0)
                        return No("a gas tank blast with no tank");
                    Barrel used = _barrels[found];
                    used.Used = true;
                    _barrels[found] = used;
                    break;
                }
                default: // an implosion's charge
                {
                    if (!Who(e, out Crew c))
                        return false;
                    bool placed = false;
                    foreach (LogEvent s in c.Strikes)
                    {
                        if (s.Action != ActionKind.Implosion || s.Picks == null || e.Time < s.Time + RankedRules.ChargeCountdown - 0.3f
                            || e.Time > s.Time + ChargesEnd(s) + RankedRules.ActionSlack)
                            continue;
                        for (int i = 0; i < s.Picks.Length && !placed; i += 2)
                            placed = Mathf.Abs(s.Picks[i] - e.X) <= 0.1f && Mathf.Abs(s.Picks[i + 1] - e.Y) <= 0.1f;
                    }
                    if (!placed)
                        return No("a charge nobody placed");
                    break;
                }
            }
            _knocks.Add(new Knock { From = e.Time, To = e.Time, X = e.X, Y = e.Y });
            return true;
        }

        private bool ActionStart(LogEvent e)
        {
            if (!Who(e, out Crew c))
                return false;
            if (e.Action != c.Action.Kind)
                return No("an action the player doesn't carry");
            if (c.Starting)
                return No("an action while one plays");
            if (c.Charge < c.Action.Charge - 0.001f)
                return No($"an action on a meter at {c.Charge:0.#} of {c.Action.Charge:0}");
            c.Charge = 0f;
            c.Idle = 0f;
            c.Starting = true;
            c.StartedAt = e.Time;
            return true;
        }

        private bool ActionStrike(LogEvent e)
        {
            if (!Who(e, out Crew c))
                return false;
            ActionInfo info = c.Action;
            if (!c.Starting || e.Action != info.Kind)
                return No("a strike with no action started");
            float lead = 0f;
            bool untilLanded = false;
            for (int i = 0; i < info.StrikeFrame; i++)
            {
                untilLanded |= info.Frames[i] < 0f;
                lead += Mathf.Max(0f, info.Frames[i]);
            }
            untilLanded |= info.Frames[info.StrikeFrame] < 0f;
            if (e.Time - c.StartedAt > lead + (untilLanded ? 6f : 0f) + RankedRules.ActionSlack + (OnlinePartner(e.Player) ? 1f : 0f))
                return No("a strike long after its action started");
            Pose pose = PoseAt(e.Player, e.Time);
            Vector2 offset = info.Kind == ActionKind.Burp ? new Vector2(0.7f, 1.5f)
                : info.Kind == ActionKind.CraneDrop ? new Vector2(5f, 0f)
                : info.Kind == ActionKind.SiteRadio ? new Vector2(0.85f, 0f) : Vector2.zero;
            float reach = Slack(e.Player) + Mathf.Abs(offset.x) + (OnlinePartner(e.Player) ? ActionGate.Reach : 0f);
            if (Mathf.Abs(e.X - pose.X) > reach || Mathf.Abs(e.Y - pose.Y - offset.y) > Slack(e.Player) + 1f)
                return No("a strike out of the player's reach");
            if (!Picks(e))
                return false;

            float after = 0f;
            for (int i = info.StrikeFrame; i < info.Frames.Length; i++)
                after += Mathf.Max(0f, info.Frames[i]);
            c.Starting = false;
            c.BusyUntil = e.Time + after + info.Hold + 0.5f;
            c.Strikes.Add(e);
            switch (info.Kind)
            {
                case ActionKind.CoffeeBreak:
                    c.CoffeeUntil = e.Time + info.Duration + 0.1f;
                    c.CoffeeFrom.Add(e.Time);
                    break;
                case ActionKind.ShovelLean:
                    for (int p = 0; p < _crew.Length; p++)
                        if (p != e.Player)
                            _crew[p].BoostUntil = e.Time + info.Duration + 0.1f;
                    break;
                case ActionKind.ButtSlam:
                    _knocks.Add(new Knock { From = e.Time, To = e.Time, X = e.X, Y = e.Y });
                    break;
                case ActionKind.CraneDrop:
                    _knocks.Add(new Knock { From = e.Time + RankedRules.CraneDelay, To = e.Time + RankedRules.CraneDelay + 8f, X = e.Picks[0], Y = e.Picks[1] });
                    break;
            }
            return true;
        }

        // What a strike chose in the world: the crane's spot and the radio's are on the ground under
        // the strike, the charges on one standing building's blocks.
        private bool Picks(LogEvent e)
        {
            switch (e.Action)
            {
                case ActionKind.CraneDrop:
                case ActionKind.SiteRadio:
                    if (e.Picks == null || e.Picks.Length != 2 || Mathf.Abs(e.Picks[0] - e.X) > 0.05f || e.Picks[1] > e.Y + 21f)
                        return No("a spot not under the strike");
                    return true;
                case ActionKind.Implosion:
                    if (e.Picks == null)
                        return true; // nothing standing in reach: the plunger goes down on nothing
                    Site on = null;
                    for (int i = 0; i < e.Picks.Length; i += 2)
                    {
                        Block block = StandingAt(e.Picks[i], e.Picks[i + 1]);
                        if (block == null || (on != null && block.Site != on))
                            return No("a charge not on the building");
                        on = block.Site;
                    }
                    float gap = Mathf.Max(0f, Mathf.Max(on.Placement.X - e.X, e.X - (on.Placement.X + on.Placement.Spec.Width)));
                    return gap <= 14f + 1f || No("charges out of reach");
                default:
                    return e.Picks == null || No("picks for an action that picks nothing");
            }
        }

        private Block StandingAt(float x, float y)
        {
            foreach (Site site in _sites)
            {
                BuildingPlacement b = site.Placement;
                var cell = new Vector2Int(Mathf.FloorToInt(x - b.X), Mathf.FloorToInt(y - b.GroundY));
                if (site.Cells.TryGetValue(cell, out Block block))
                    return block;
            }
            return null;
        }

        private bool Out(LogEvent e)
        {
            if (!Who(e, out Crew c))
                return false;
            if (e.Rejoins && _log.Mode != RankedMode.Coop)
                return No("a rejoin in solo");
            c.Out = true;
            c.OutAt = e.Time;
            c.Thrown = null;
            if (e.Rejoins)
                _score.ApplyPenalty(e.Player, RankedRules.RejoinPenalty);
            return true;
        }

        private bool Rejoin(LogEvent e)
        {
            Crew c = e.Player >= 0 && e.Player < _crew.Length ? _crew[e.Player] : null;
            if (c == null || !c.Out || _log.Mode != RankedMode.Coop)
                return No("a rejoin by a player in play");
            Pose partner = PoseAt(1 - e.Player, e.Time);
            Pose own = PoseAt(e.Player, e.Time);
            float slack = Slack(0) + Slack(1) + 1f;
            bool byPartner = Mathf.Abs(e.X - partner.X - RankedRules.RejoinOffsetX) <= slack && Mathf.Abs(e.Y - partner.Y - RankedRules.RejoinOffsetY) <= slack;
            if (!byPartner && Vector2.Distance(new Vector2(e.X, e.Y), new Vector2(own.X, own.Y)) > slack)
                return No("a rejoin away from the partner");
            c.Out = false;
            c.OutSpans.Add(new Vector2(c.OutAt, e.Time));
            return true;
        }

        private bool Finish(LogEvent e)
        {
            bool there = false;
            for (int p = 0; p < _crew.Length; p++)
                there |= !_crew[p].Out && PoseAt(p, e.Time).X >= _layout.FinishX - 1f - Slack(p);
            if (!there)
                return No("a finish away from the finish line");
            // Running flat out all the way, with every coffee drunk, takes at least this long.
            float distance = _layout.FinishX - _layout.SpawnX;
            float coffee = 0f;
            foreach (Crew c in _crew)
                coffee = Mathf.Max(coffee, c.CoffeeFrom.Count * ActionCatalog.Get(ActionKind.CoffeeBreak).Duration
                    * RankedRules.RunSpeed * (RankedRules.CoffeeSpeed - 1f));
            if (e.Time < (distance - coffee) / (RankedRules.RunSpeed * 1.1f))
                return No("a finish sooner than running there takes");
            _ended = true;
            _verdict.Finished = true;
            _verdict.TimeMs = e.TimeMs;
            return true;
        }

        // ---------------- The pose track ----------------

        private bool Movement()
        {
            float end = _verdict.TimeMs / 1000f;
            int samples = Mathf.FloorToInt(end / RunLog.PoseInterval);
            for (int p = 0; p < _crew.Length; p++)
            {
                Crew c = _crew[p];
                if (c.Out)
                    c.OutSpans.Add(new Vector2(c.OutAt, float.PositiveInfinity));
                List<Pose> poses = _log.Poses[p];
                if (poses.Count < samples - 1)
                    return No($"player {p + 1}'s pose track ends early");
                if (poses.Count == 0)
                    continue;
                if (Mathf.Abs(poses[0].X - (_layout.SpawnX + 0.5f)) > 8f)
                    return No($"player {p + 1} didn't start at the start");
                int rising = 0;
                float riseLimit = RankedRules.JumpSpeed / RankedRules.Gravity + 0.35f;
                float lowest = _layout.LowestGround - LevelLayout.GroundDepth - 2f;
                for (int i = 1; i < poses.Count && i <= samples; i++)
                {
                    float t = i * RunLog.PoseInterval;
                    Pose a = poses[i - 1], b = poses[i];
                    if (OutBetween(c, t - RunLog.PoseInterval - 0.05f, t + 0.05f))
                    {
                        rising = 0;
                        continue;
                    }
                    if (b.Y < lowest && !OutBetween(c, t, t + 3.5f)) // the out-of-bounds grace is 3 s
                        return No($"player {p + 1} fell out of the level and stayed in play");
                    bool knocked = Knocked(b, t);
                    float speed = RankedRules.RunSpeed * (CoffeeIn(c, t - RunLog.PoseInterval, t) ? RankedRules.CoffeeSpeed : 1f);
                    float step = speed * RunLog.PoseInterval * RankedRules.SpeedSlack + RankedRules.MoveMargin + (knocked ? 2f : 0f)
                        + (OnlinePartner(p) ? 0.5f : 0f);
                    if (Mathf.Abs(b.X - a.X) > step)
                        return No($"player {p + 1} moved {Mathf.Abs(b.X - a.X):0.0} in 0.1s at {t:0.0}s");
                    float up = RankedRules.JumpSpeed * RunLog.PoseInterval * RankedRules.SpeedSlack + RankedRules.MoveMargin + (knocked ? 2f : 0f)
                        + (OnlinePartner(p) ? 0.5f : 0f);
                    if (b.Y - a.Y > up)
                        return No($"player {p + 1} rose {b.Y - a.Y:0.0} in 0.1s at {t:0.0}s");
                    rising = b.Y - a.Y > 0.05f && !knocked ? rising + 1 : 0;
                    if (rising * RunLog.PoseInterval > riseLimit && !OnlinePartner(p))
                        return No($"player {p + 1} kept rising at {t:0.0}s");
                }
            }
            return true;
        }

        private static bool OutBetween(Crew c, float from, float to)
        {
            foreach (Vector2 span in c.OutSpans)
                if (span.x <= to && span.y >= from)
                    return true;
            return c.Out && c.OutAt <= to;
        }

        private bool Knocked(Pose pose, float time)
        {
            foreach (Knock k in _knocks)
                if (time >= k.From && time <= k.To + RankedRules.KnockbackWindow
                    && Vector2.Distance(new Vector2(pose.X, pose.Y), new Vector2(k.X, k.Y)) <= RankedRules.KnockbackRange + RankedRules.RunSpeed * RankedRules.KnockbackWindow)
                    return true;
            return false;
        }

        // ---------------- Helpers ----------------

        private Pose PoseAt(int player, float time)
        {
            List<Pose> poses = _log.Poses[player];
            if (poses.Count == 0)
                return new Pose(float.NaN, float.NaN, false);
            float at = time / RunLog.PoseInterval;
            int i = Mathf.Clamp(Mathf.FloorToInt(at), 0, poses.Count - 1);
            int j = Mathf.Min(i + 1, poses.Count - 1);
            float f = Mathf.Clamp01(at - i);
            return new Pose(Mathf.Lerp(poses[i].X, poses[j].X, f), Mathf.Lerp(poses[i].Y, poses[j].Y, f), poses[i].Left);
        }

        private bool OnlinePartner(int player) => _log.Online && player == 1;

        private float Slack(int player) =>
            RankedRules.PoseSlack + (OnlinePartner(player) ? RankedRules.RunSpeed * RankedRules.CoffeeSpeed * RankedRules.OnlineLag : 0f);

        private static float Buffs(Crew c, float time) =>
            (time <= c.CoffeeUntil ? RankedRules.CoffeeDamage : 1f) * (time <= c.BoostUntil ? RankedRules.BoostDamage : 1f);

        private static bool CoffeeIn(Crew c, float from, float to)
        {
            float duration = ActionCatalog.Get(ActionKind.CoffeeBreak).Duration + 0.1f;
            foreach (float start in c.CoffeeFrom)
                if (start <= to && start + duration >= from)
                    return true;
            return false;
        }

        private static float Distance(float x, float y, Block block)
        {
            float dx = Mathf.Max(0f, Mathf.Max(block.X0 - x, x - block.X1));
            float dy = Mathf.Max(0f, Mathf.Max(block.Y0 - y, y - block.Y1));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool Overlaps(Block block, float x0, float y0, float x1, float y1) =>
            block.X1 >= x0 - 0.05f && block.X0 <= x1 + 0.05f && block.Y1 >= y0 - 0.05f && block.Y0 <= y1 + 0.05f;
    }
}
