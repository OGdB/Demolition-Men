using System;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Ranked
{
    /// <summary>
    /// Writes a ranked run's log (docs/ranked.md §3) on the device that decides the run: offline, or
    /// the host online, where the partner's hits, sticks and pickups arrive as messages. Whatever
    /// damages a standing block, takes a pickup, throws or sets off a stick, or starts or strikes an
    /// action calls a hook here; outside a ranked run (or on a client) the hooks do nothing.
    /// Positions are the level's: the Level object stands at the origin.
    /// </summary>
    public sealed class RunRecorder : MonoBehaviour
    {
        /// <summary>The ranked run being recorded, or null.</summary>
        public static RunRecorder Current { get; private set; }

        /// <summary>A ranked run ended: its log is complete.</summary>
        public static event Action<RunLog> Completed;

        public RunLog Log { get; private set; }
        public bool Complete { get; private set; }

        private RunController _run;
        private LevelLayout _layout;
        private RankedDay _day;
        private bool _recording;
        private int _sample;
        private Vector2[] _last;
        private float _lastAt;

        /// <summary>Record <paramref name="run"/> of <paramref name="layout"/>, the level of <paramref name="day"/>, from GO.</summary>
        public void Begin(RunController run, LevelLayout layout, RankedDay day)
        {
            _run = run;
            _layout = layout;
            _day = day;
            Current = this;
            // After the run's own handlers: a rejoined player stands where the run revived them.
            run.State.PhaseChanged += OnPhase;
            run.State.PlayerOut += OnOut;
            run.State.PlayerRejoined += OnRejoined;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (_run != null && _run.State != null)
            {
                _run.State.PhaseChanged -= OnPhase;
                _run.State.PlayerOut -= OnOut;
                _run.State.PlayerRejoined -= OnRejoined;
            }
        }

        private void OnPhase(RunPhase phase)
        {
            if (phase == RunPhase.Playing && Log == null)
            {
                Log = RunLog.For(_run.Players.Count);
                Log.Rules = RankedRules.Version;
                Log.Mode = _run.Players.Count > 1 ? RankedMode.Coop : RankedMode.Solo; // the crew that plays decides the board
                Log.Online = NetRole.IsOnline;
                Log.Day = _day.Day;
                Log.Template = _day.Template;
                Log.Seed = _day.Seed;
                Log.LayoutHash = _layout.Hash();
                _last = new Vector2[_run.Players.Count];
                for (int i = 0; i < _last.Length; i++)
                    _last[i] = _run.Players[i].Transform.position;
                _lastAt = _run.State.Elapsed;
                _recording = true;
                Sample();
            }
            else if ((phase == RunPhase.Finished || phase == RunPhase.Failed) && _recording)
            {
                End(phase == RunPhase.Finished);
            }
        }

        private void Update()
        {
            if (_recording)
                Sample();
        }

        // Every pose due by now; a long frame's missed ones lie on the way from the last.
        private void Sample()
        {
            float now = _run.State.Elapsed;
            for (; _sample * RunLog.PoseInterval <= now; _sample++)
            {
                float at = _sample * RunLog.PoseInterval;
                float f = now > _lastAt ? Mathf.Clamp01((at - _lastAt) / (now - _lastAt)) : 1f;
                for (int p = 0; p < _last.Length; p++)
                {
                    RunController.Player player = _run.Players[p];
                    Vector2 pos = Vector2.Lerp(_last[p], player.Transform.position, f);
                    Log.Poses[p].Add(new LogPose(pos.x, pos.y, player.Motor != null && player.Motor.Facing < 0));
                }
            }
            for (int p = 0; p < _last.Length; p++)
                _last[p] = _run.Players[p].Transform.position;
            _lastAt = now;
        }

        private void End(bool finished)
        {
            Sample();
            Add(new LogEvent { Kind = finished ? LogKind.Finish : LogKind.Fail });
            _recording = false;
            Log.TimeMs = Now;
            // The score as the server will count it: of the logged time, to the millisecond.
            Log.Score = finished ? _run.Score.FinalTotal(Log.TimeMs / 1000f, _layout.Par) : 0;
            for (int p = 0; p < Log.Players; p++)
                if (_run.Players[p].Action != null && _run.Players[p].Action.Info != null)
                    Log.Actions[p] = _run.Players[p].Action.Info.Kind; // online the partner's arrives during the countdown
            Complete = true;
            if (Debug.isDebugBuild)
            {
                // What the server will say, while the server isn't there yet (docs/ranked.md K3).
                try
                {
                    byte[] bytes = Log.Encode();
                    Verdict verdict = RunVerifier.Verify(RunLog.Decode(bytes), _layout);
                    Debug.Log($"[Ranked] {(verdict.Ok ? "passes" : "turned down: " + verdict.Reason + (verdict.Event >= 0 ? $" (event {verdict.Event})" : ""))}" +
                        $"; score {verdict.Score} (device {Log.Score}), {Log.Events.Count} events, {bytes.Length} bytes");
                }
                catch (Exception e)
                {
                    Debug.LogError("[Ranked] the log doesn't read back: " + e.Message);
                }
            }
            Completed?.Invoke(Log);
        }

        // ---------------- Hooks ----------------

        private static bool On => Current != null && Current._recording;

        private static int Now => Mathf.RoundToInt(Current._run.State.Elapsed * 1000f);

        private static void Add(LogEvent e)
        {
            e.TimeMs = Now;
            Current.Log.Events.Add(e);
        }

        private void OnOut(int player, bool rejoins)
        {
            if (_recording)
                Add(new LogEvent { Kind = LogKind.Out, Player = player, Rejoins = rejoins });
        }

        private void OnRejoined(int player)
        {
            if (!_recording)
                return;
            Vector2 at = _run.Players[player].Transform.position;
            Add(new LogEvent { Kind = LogKind.Rejoin, Player = player, X = at.x, Y = at.y });
        }

        /// <summary>A standing block took <paramref name="amount"/> (BuildingBlock.TakeDamage).</summary>
        public static void Damaged(BuildingBlock block, float amount, int instigator, HitCause cause, Vector2 from, bool broke)
        {
            DestructibleBuilding building = block.Building;
            if (!On || building == null || building.NetIndex < 0 || block.Coords.Count == 0)
                return;
            Vector2Int cell = block.Coords[0];
            Add(new LogEvent
            {
                Kind = LogKind.Damage, Player = instigator, Building = building.NetIndex, CellX = cell.x, CellY = cell.y,
                Amount = amount, Cause = cause, Broke = broke, X = from.x, Y = from.y,
            });
        }

        public static void PickedUp(int index, int player)
        {
            if (On && index >= 0)
                Add(new LogEvent { Kind = LogKind.Pickup, Player = player, Index = index });
        }

        public static void Threw(int player, Vector2 from)
        {
            if (On)
                Add(new LogEvent { Kind = LogKind.Throw, Player = player, X = from.x, Y = from.y });
        }

        public static void Blew(int player, HitCause cause, Vector2 at)
        {
            if (On)
                Add(new LogEvent { Kind = LogKind.Blast, Player = player, Cause = cause, X = at.x, Y = at.y });
        }

        public static void ActionStarted(int player, ActionKind kind)
        {
            if (On)
                Add(new LogEvent { Kind = LogKind.ActionStart, Player = player, Action = kind });
        }

        public static void ActionStruck(int player, ActionKind kind, Vector2 at, int facing, Vector2[] spots)
        {
            if (!On)
                return;
            float[] picks = null;
            if (spots != null && spots.Length > 0)
            {
                int count = Mathf.Min(spots.Length, RunLog.MaxPicks);
                picks = new float[count * 2];
                for (int i = 0; i < count; i++)
                {
                    picks[i * 2] = spots[i].x;
                    picks[i * 2 + 1] = spots[i].y;
                }
            }
            Add(new LogEvent { Kind = LogKind.ActionStrike, Player = player, Action = kind, X = at.x, Y = at.y, Facing = facing, Picks = picks });
        }
    }
}
