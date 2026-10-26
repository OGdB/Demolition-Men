using Demolition.Actions;
using Demolition.Levels;
using Demolition.Run;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// An autopilot for a player (roadmap M3 bot playtester, the Clip Lab's bot footage): runs
    /// right, hits whatever building block is in the way, jumps up steps, over rubble and over
    /// pits, and works itself loose when stuck (a hop, then digging, backing off and a running
    /// jump in turn). It plays safe the way a player
    /// learns to: dynamite only from outside its blast, at what fists barely dent (supports,
    /// metal) and at gas tanks; never a punch at a gas tank (it jumps them); out from under a
    /// collapse; and never so far back that the scrolling camera leaves it behind. Once its action
    /// meter is full it fires the action where a player would (<see cref="BotTactics"/>) and
    /// stays out of its strike (the charges' 3-2-1, the crane's pallet). Walking to a
    /// point (<see cref="StopX"/>, the Clip Lab) it only clears the way. Drives the player
    /// through a <see cref="ScriptedInputSource"/>, exactly like a real input device. It logs
    /// what an agent tuning it needs ("Bot: " lines): every action it fires; every hit it takes,
    /// what dealt it and what was around; leaving the screen; its death, why, with its last
    /// six seconds; and how the run ended.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerCombat))]
    public sealed class BotPilot : MonoBehaviour
    {
        private const float StuckSeconds = 1.2f;
        /// <summary>Stuck this long (a heap it can't climb, no room to jump), it digs, backs off and
        /// takes a running jump, in turn, every <see cref="UnstickCycle"/> seconds.</summary>
        private const float DigSeconds = 3f;
        private const float UnstickCycle = 2.4f;
        /// <summary>Hold jump this long: a one-frame tap can fall between physics steps.</summary>
        private const float JumpHold = 0.25f;
        /// <summary>How far ahead of the feet a swing reaches (the far edge of <see cref="BlockAhead"/>).</summary>
        private const float Reach = 1.5f;
        /// <summary>Dynamite is thrown at a block this far from the feet: the stick goes off at the
        /// block's face and hurts players within 3 units of it, and much further it lands short.</summary>
        private const float ThrowMin = 4f;
        private const float ThrowMax = 5.8f;
        /// <summary>A retreat stops this far inside the camera's left edge (out of bounds past it).</summary>
        private const float EdgeMargin = 1.5f;
        /// <summary>This close to the camera's edge, standing still is death (the grace is 3 s) and a
        /// block from above a maybe: it runs on, whatever falls or is still to go off.</summary>
        private const float Pressed = EdgeMargin + 2f;
        /// <summary>Seconds of room a throw needs: it stands through the fuse and the blast.</summary>
        private const float ThrowRoom = 4f;
        /// <summary>The stick flies out at chest height and rises: a block over the first few units at
        /// the top of its arc (a floor left standing over a burped-out ground floor) sets it off in
        /// the thrower's face.</summary>
        private const float ArcHeight = 2.1f, ArcReach = 3.5f;
        /// <summary>The blast flings debris well past its own 3 units: step back from a stick this near.</summary>
        private const float StickClear = 6.5f;
        /// <summary>Move input that turns the player without pushing them: under the motor's 0.2
        /// force threshold, over the facing deadzone. Letting go only coasts 7 units from full speed.</summary>
        private const float Face = 0.15f;
        /// <summary>Seconds of room it stands punching a support in reach (walk-through, so it would run on).</summary>
        private const float SupportRoom = 6f;
        private static readonly float[] ThrowHeights = { 0.5f, 1.3f };
        /// <summary>How far ahead it looks for a building to fire its action at.</summary>
        private const float ActionReach = BotTactics.ChargeReach + 1f;
        /// <summary>The crane's pallet lands this far ahead (<see cref="ActionUser"/>'s strike point).</summary>
        private const float CraneReach = 5f;
        private const float SightEvery = 0.15f;
        /// <summary>After calling the crane or setting the charges, it watches for them this long.</summary>
        private const float StrikeWatch = 8f;

        private static readonly ContactFilter2D Solid = new ContactFilter2D { useTriggers = false };
        private static readonly ContactFilter2D Any = new ContactFilter2D { useTriggers = true };
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private readonly Collider2D[] _overlaps = new Collider2D[64];
        private float _jumpUntil;

        // A new jump only once the last one has been let go for a moment (a fresh press edge).
        private bool CanArmJump => Time.time >= _jumpUntil + 0.05f;

        private readonly ScriptedInputSource _source = new ScriptedInputSource();
        private LevelBuilder _level; // its map knows the pits (none on the test bench)
        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private PlayerHealth _health;
        private Rigidbody2D _body;
        private ActionUser _action; // added by the run when it finds the players, after this wakes
        private float _fullSince;    // the action meter filled
        private float _sightAt;
        private float _strikeUntil;
        private float _brakeUntil;   // stopping to aim its action
        private float _pressedUntil; // running on from the camera's edge
        private string _fatalHit; // the hit that took its last health
        private bool _loggedDeath, _loggedEnd, _offScreen;
        // What it was doing over the last six seconds, oldest first from _historyAt.
        private readonly string[] _history = new string[24];
        private int _historyAt;
        private float _historyNext;
        private float _progressX;
        private float _progressTime; // last progress or stuck hop
        private float _stuckSince;   // last progress
        private bool _jumpHeld;
        private float _backUntil;
        private float _escape = -1f; // which way out from under the debris
        // Stepping back for a run at a throw: back to this x, then in again.
        private float _runUpFrom = float.NegativeInfinity;
        private float _runUpUntil;
        private BuildingBlock _runUpTarget;
        private int _runUps;

        /// <summary>Stop once a swing would reach this world x (the Clip Lab walks players up to a
        /// block without hitting it). Infinity: run to the finish.</summary>
        public float StopX { get; set; } = float.PositiveInfinity;
        /// <summary>Throws dynamite at the supports, metal and gas tanks it meets.</summary>
        public bool ThrowsDynamite { get; set; } = true;
        public bool Arrived { get; private set; }
        /// <summary>What it decided this frame, for watching it from tools.</summary>
        public string Status { get; private set; } = "";

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _health = GetComponent<PlayerHealth>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.Hurt += OnHurt;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Hurt -= OnHurt;
        }

        // In Start: the run assigns devices to players when it wakes, after this was added.
        private void Start()
        {
            GetComponent<PlayerInputReader>().Source = _source;
            _level = FindAnyObjectByType<LevelBuilder>();
        }

        private void Update()
        {
            Vector2 feet = transform.position;
            float now = Time.time;
            RunController run = RunController.Current;
            bool over = run != null && run.State.IsOver;
            LogHealth(feet, run);
            if (over && !_loggedEnd)
            {
                _loggedEnd = true;
                Debug.Log($"Bot: {name} run over: {run.State.Phase} after {run.State.Elapsed:0}s at x {feet.x:0.0}");
            }
            if (feet.x + Reach >= StopX || over)
            {
                // Arrived, or the run is over: hands off (on the results screen it would punch the air).
                Arrived |= feet.x + Reach >= StopX;
                Status = over ? "run over" : "arrived";
                _source.Next = new PlayerInputFrame { JumpReleased = _jumpHeld };
                _jumpHeld = false;
                return;
            }
            if (_action == null)
                _action = GetComponent<ActionUser>();
            if (_action != null && _action.Busy)
            {
                // Its action's frames have the controls: not stuck, nothing to decide.
                _progressX = feet.x;
                _progressTime = _stuckSince = now;
                Status = $"action {_action.Info.Id}";
                return;
            }

            BuildingBlock block = BlockAhead(feet);
            bool headroom = Headroom(feet);
            // A heap in the way gets punched through as a player would, rather than walked into:
            // a step of it is jumped where there's room overhead (under a floor there isn't).
            bool attack = block != null || HeapAhead(feet, !headroom);
            bool throwNow = false;
            bool fire = false;
            float move = 1f;
            if (float.IsPositiveInfinity(StopX))
            {
                if (block != null && block.Explosive)
                    attack = false; // the punch that breaks it goes off in our face: jumped, or blown up from afar
                throwNow = PlanThrow(feet, now, ref move);
                bool falling = DebrisAbove(feet, out float escape);
                bool tumbling = DebrisAhead(feet);
                if (falling)
                {
                    _backUntil = now + 0.35f;
                    // Forward only if the way is open, and never towards a pit's edge.
                    bool open = block == null && !PitAt(feet + new Vector2(1.2f, 0.2f)) && !PitAt(feet + new Vector2(3f, 0.2f));
                    _escape = escape > 0f && !open ? -1f : escape;
                }
                if (now < _backUntil)
                    move = block != null ? -1f : _escape; // a forward escape that meets a wall backs off: no punching under the fall
                else if (move > 0f && tumbling)
                    move = 0f; // tumbling blocks ahead may yet come down on its head
                else if (_combat.DynamiteOut || StrikePending(now))
                    move = Mathf.Min(move, StickNear(feet) ? -1f : 0f); // ours is about to go off ahead: out of its reach, and out of what its blast flings
                if (move < 0f)
                {
                    attack = false;
                    move = Back(feet);
                }
                if (attack && move > 0f && block != null && block.Material == BlockMaterial.Support && Room(feet) > SupportRoom && Daring(feet))
                    move = 0f; // a support is walk-through: stand and finish it, the building comes down with it
                fire = !throwNow && PlanAction(feet, now, headroom, falling || tumbling);
                if (now < _brakeUntil)
                {
                    move = Brake(Face); // stopping to aim, then facing ahead without pushing
                    attack = false;
                }
                if (feet.x - CameraLeft() < Pressed)
                    _pressedUntil = now + 1f;
                if (move < 0.5f && now < _pressedUntil)
                {
                    // Pinned against the camera's edge: run on, hitting what's in the way.
                    move = 1f;
                    attack = (block != null && !block.Explosive) || HeapAhead(feet, !headroom);
                }
            }

            // Standing back on purpose, or braking, isn't being stuck.
            if (move < 0.5f || feet.x > _progressX + 0.5f)
            {
                _progressX = feet.x;
                _progressTime = _stuckSince = now;
            }
            // Stuck a while (a heap it can't climb, debris on its head): swing at what's in front,
            // then step back and take a running jump at it, and round again.
            bool unstickJump = false;
            float stuckFor = now - _stuckSince;
            if (stuckFor > DigSeconds)
            {
                float phase = (stuckFor - DigSeconds) % UnstickCycle;
                attack = phase < 1.2f;
                if (phase >= 1.2f && phase < 1.8f)
                    move = Back(feet);
                unstickJump = phase >= 1.8f && phase < 1.9f;
            }
            if (attack && move >= 0f && _motor.Facing < 0)
                attack = false; // a swing locks the facing: turn round first, or it punches the air behind

            bool grounded = _motor.IsGrounded;
            bool pit = PitAhead(feet);
            // Under a wall's overhang (a doorway) a jump only bumps the head: out from under it
            // first, and wait at a pit's edge for that rather than walk into it.
            if (pit && !headroom && move > 0f)
                move = Brake(0f);
            if (CanArmJump && grounded && move > 0f && !attack && headroom
                && (pit || ((StepAhead(feet) || ExplosiveAhead(feet)) && LandingClear(feet))))
                _jumpUntil = now + JumpHold;
            else if (move > 0f && grounded && CanArmJump && LandingClear(feet) && (unstickJump || (headroom && now - _progressTime > StuckSeconds)))
            {
                _jumpUntil = now + JumpHold;
                _progressTime = now;
            }
            bool jump = now < _jumpUntil;
            if (move == 0f && grounded)
                move = Brake(0f); // standing means standing: letting go coasts 7 units, over a pit's edge too
            Status = $"move {move:0} attack {attack} jump {jump} throw {throwNow} head {headroom} pit {pit} gnd {grounded} ahead {(block != null ? $"{block.Material}{(block.Explosive ? "!" : "")} {block.HealthFraction:0.00}" : "-")}"
                     + $" back {Mathf.Max(0f, _backUntil - now):0.0} runUp {(float.IsNegativeInfinity(_runUpFrom) ? "-" : $"{_runUpFrom:0.0}")} out {_combat.DynamiteOut} tnt {_combat.DynamiteCount}"
                     + $" action {(_action != null ? $"{_action.Info.Id} {_action.Meter.Fraction:0.00}{(fire ? " FIRE" : "")}" : "-")}";

            _source.Next.Move = new Vector2(move, 0f);
            _source.Next.AttackHeld = attack;
            _source.Next.DynamitePressed = throwNow;
            _source.Next.ActionPressed = fire;
            _source.Next.JumpPressed = jump && !_jumpHeld;
            _source.Next.JumpReleased = !jump && _jumpHeld;
            _source.Next.JumpHeld = jump;
            _jumpHeld = jump;
        }

        // A stick for the block ahead when it deserves one: thrown from between ThrowMin and
        // ThrowMax, after stepping back for a run at it when the block is closer than that (the
        // throw goes the way the player faces, so it comes walking in).
        private bool PlanThrow(Vector2 feet, float now, ref float move)
        {
            if (!ThrowsDynamite || _combat.DynamiteCount == 0 || _combat.DynamiteOut || Room(feet) < ThrowRoom)
                return false;
            BuildingBlock target = ThrowTarget(feet, out float distance);
            if (target == null)
            {
                _runUpFrom = float.NegativeInfinity;
                return false;
            }
            if (target != _runUpTarget)
            {
                _runUpTarget = target;
                _runUps = 0;
            }
            if (!float.IsNegativeInfinity(_runUpFrom))
            {
                if (feet.x > _runUpFrom && now < _runUpUntil) // something behind can cut it short
                {
                    move = -1f;
                    return false;
                }
                _runUpFrom = float.NegativeInfinity;
            }
            if (distance > ThrowMax)
                return false;
            if (distance >= ThrowMin)
            {
                // Stand and throw, once facing it (after backing off it still faces away).
                move = _motor.Facing > 0 ? 0f : 1f;
                if (_motor.Facing < 0 || !_motor.IsGrounded || now < _backUntil)
                    return false;
                _runUpTarget = null;
                return true;
            }
            // Too close: back off out of the blast, twice at most (then fists it is), never off screen.
            float from = feet.x - (ThrowMin + 0.8f - distance);
            if (distance < ThrowMin && _runUps < 2 && from > CameraLeft() + EdgeMargin && !PitAt(new Vector2(from, feet.y + 0.2f)))
            {
                _runUps++;
                _runUpFrom = from;
                _runUpUntil = now + 1f;
                move = -1f;
            }
            return false;
        }

        // The nearest standing block straight ahead at waist or chest height, if it's worth a stick
        // and nothing loose lies in the stick's way (it goes off on the first block it touches).
        private BuildingBlock ThrowTarget(Vector2 feet, out float distance)
        {
            BuildingBlock nearest = NearestAhead(feet, ThrowMax + 1f, out distance, out float loose);
            return nearest != null && loose > distance && ArcClear(feet) && HandClear(feet) && WorthAStick(nearest) ? nearest : null;
        }

        // Nothing where the stick appears (a unit ahead at chest height, PlayerCombat's throw): it
        // goes off at once on a block overlapping its trigger, in the thrower's face.
        private bool HandClear(Vector2 feet)
        {
            int count = Physics2D.OverlapBox(feet + new Vector2(1f, 1.3f), new Vector2(1f, 1.1f), 0f, Any, _overlaps);
            for (int i = 0; i < count; i++)
                if (_overlaps[i].GetComponentInParent<BuildingBlock>() != null)
                    return false;
            return true;
        }

        private bool ArcClear(Vector2 feet)
        {
            int count = Physics2D.Raycast(feet + new Vector2(0.3f, ArcHeight), Vector2.right, Any, _hits, ArcReach);
            for (int i = 0; i < count; i++)
                if (_hits[i].collider.GetComponentInParent<BuildingBlock>() != null)
                    return false;
            return true;
        }

        // The nearest standing block straight ahead at waist or chest height, within reach, and how
        // near the first loose block (rubble, or one still falling) in the way comes.
        private BuildingBlock NearestAhead(Vector2 feet, float reach, out float distance, out float loose)
        {
            distance = loose = float.PositiveInfinity;
            BuildingBlock nearest = null;
            foreach (float height in ThrowHeights)
            {
                int count = Physics2D.Raycast(feet + new Vector2(0.3f, height), Vector2.right, Any, _hits, reach);
                for (int i = 0; i < count; i++)
                {
                    Collider2D hit = _hits[i].collider;
                    if (hit.GetComponentInParent<PlayerHealth>() != null)
                        continue;
                    var block = hit.GetComponentInParent<BuildingBlock>();
                    if (block == null && hit.isTrigger)
                        continue; // pickups, the finish line
                    float near = _hits[i].distance + 0.3f;
                    if (block != null && (block.IsRubble || block.IsFalling))
                        loose = Mathf.Min(loose, near); // the stick goes off on anything loose in its way
                    if (block != null && block.IsFalling)
                        continue; // the sight looks past what's coming down
                    // The first solid thing in the way: the stick goes off on it (or stops at a wall of ground).
                    if (block != null && near < distance)
                    {
                        distance = near;
                        nearest = block;
                    }
                    break;
                }
            }
            return nearest;
        }

        // ---------------- The action ----------------

        // Fire once the meter is full, where BotTactics says a player would (looked at a few times a second: the charges' pick searches every building).
        private bool PlanAction(Vector2 feet, float now, bool headroom, bool debris)
        {
            if (_action == null || !_action.Meter.Full)
            {
                _fullSince = now;
                return false;
            }
            if (now < _sightAt)
                return false;
            _sightAt = now + SightEvery;

            ActionKind kind = _action.Info.Kind;
            BuildingBlock wall = NearestAhead(feet, ActionReach, out float distance, out _);
            var sight = new BotSight
            {
                FacingAhead = _motor.Facing > 0,
                Grounded = _motor.IsGrounded,
                Headroom = headroom,
                Debris = debris,
                Retreating = now < _backUntil || _combat.DynamiteOut || StrikePending(now),
                Health = _health != null && _health.Max > 0f ? _health.Current / _health.Max : 1f,
                Wall = distance,
                WallIsBuilding = wall != null && wall.Building != null && !IsProp(wall.Building),
                FloorsBelow = FloorsBelow(feet),
                Room = Room(feet),
                PartnerWorking = PartnerWorking(feet),
                FullFor = now - _fullSince,
                Speed = _body != null ? Mathf.Abs(_body.linearVelocity.x) : 0f,
            };
            if (kind == ActionKind.CraneDrop)
                sight.CraneTarget = StandingAt(CraneCall.Pick(feet + new Vector2(CraneReach, 0f)) + new Vector2(0f, -0.3f));
            if (kind == ActionKind.Implosion)
                sight.Charges = Charges(feet, out sight.NearestCharge);
            if (BotTactics.Fire(kind, sight))
                Debug.Log($"Bot: {name} fires {_action.Info.Id} at x {feet.x:0.0}: wall {sight.Wall:0.0}{(sight.WallIsBuilding ? " building" : "")}"
                          + $" floors below {sight.FloorsBelow} charges {sight.Charges} nearest {sight.NearestCharge:0.0} health {sight.Health:0.00}"
                          + $" room {sight.Room:0.0}s debris {sight.Debris} full for {sight.FullFor:0.0}s");
            else
            {
                sight.Speed = 0f;
                sight.FacingAhead = true;
                if (BotTactics.Fire(kind, sight))
                    _brakeUntil = now + SightEvery * 2f; // the right spot: stop there to aim it
                return false;
            }
            if (kind == ActionKind.CraneDrop || kind == ActionKind.Implosion)
                _strikeUntil = now + StrikeWatch;
            if (kind == ActionKind.Burp)
            {
                // The floors over the wrecked band come down right in front: a step back, then wait for the dust.
                float frames = BotTactics.LockSeconds(_action.Info);
                _escape = -1f;
                _backUntil = now + frames + 0.3f;
                _brakeUntil = now + frames + BotTactics.BurpWait;
            }
            return true;
        }

        // A hit, what dealt it, what it was doing (the frame before) and what was around.
        private void OnHurt(float amount, string source)
        {
            Vector2 feet = transform.position;
            if (_health.Current <= 0f)
                _fatalHit = source;
            Debug.Log($"Bot: {name} hurt {amount:0} to {_health.Current:0}/{_health.Max:0} by {source} at x {feet.x:0.0} y {feet.y:0.0}"
                      + $"\n  doing: {Status}\n  around: {Surroundings(feet)}");
        }

        // Leaving the screen, its death and why, with what it was doing in the seconds before.
        private void LogHealth(Vector2 feet, RunController run)
        {
            if (_health == null)
                return;
            float now = Time.time;
            if (now >= _historyNext)
            {
                _historyNext = now + 0.25f;
                _history[_historyAt] = $"{(run != null ? run.State.Elapsed : now):0.00}s x {feet.x:0.0} y {feet.y:0.0} hp {_health.Current:0}: {Status}";
                _historyAt = (_historyAt + 1) % _history.Length;
            }

            bool off = !_health.IsDead && feet.x < CameraLeft();
            if (off && !_offScreen)
                Debug.Log($"Bot: {name} off screen at x {feet.x:0.0}: {Status}\n  around: {Surroundings(feet)}");
            _offScreen = off;

            if (_health.IsDead && !_loggedDeath)
            {
                _loggedDeath = true;
                string why = _fatalHit != null ? $"health ran out: {_fatalHit}" : "taken out of play with health left: the camera's edge or a pit";
                var log = new System.Text.StringBuilder($"Bot: {name} died ({why}) at x {feet.x:0.0} y {feet.y:0.0}"
                    + $"{(run != null ? $" after {run.State.Elapsed:0}s" : "")}\n  around: {Surroundings(feet)}\n  last seconds:");
                for (int i = 0; i < _history.Length; i++)
                {
                    string line = _history[(_historyAt + i) % _history.Length];
                    if (line != null)
                        log.Append("\n    ").Append(line);
                }
                Debug.LogWarning(log.ToString());
            }
            else if (!_health.IsDead)
            {
                _loggedDeath = false; // revived (co-op)
                _fatalHit = null;
            }
        }

        // What is around it, for the log: the debris near and how it moves, the strike or
        // dynamite still to go off, room overhead, the ground, and the camera's edge.
        private string Surroundings(Vector2 feet)
        {
            int falling = 0;
            float best = float.MaxValue;
            Vector2 at = default, moving = default;
            int count = Physics2D.OverlapCircle(feet + Vector2.up, 4f, Solid, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block == null || !block.IsFalling)
                    continue;
                falling++;
                Vector2 offset = (Vector2)block.transform.position - feet;
                if (offset.sqrMagnitude < best)
                {
                    best = offset.sqrMagnitude;
                    at = offset;
                    moving = _overlaps[i].attachedRigidbody != null ? _overlaps[i].attachedRigidbody.linearVelocity : Vector2.zero;
                }
            }
            float left = CameraLeft();
            return $"falling blocks {falling}{(falling > 0 ? $" (nearest at {Vec(at)} moving {Vec(moving)})" : "")}"
                   + $", block ahead {(BlockAhead(feet) is BuildingBlock b ? b.Material.ToString() : "-")}, headroom {Headroom(feet)}"
                   + $", pit under {PitAt(feet + new Vector2(0f, 0.2f))} ahead {PitAhead(feet)}"
                   + $", dynamite out {_combat.DynamiteOut}, strike pending {StrikePending(Time.time)}"
                   + (_action != null ? $", action {_action.Info.Id} {_action.Meter.Fraction:0.00}{(_action.Busy ? " playing" : "")}" : "")
                   + (float.IsNegativeInfinity(left) ? "" : $", camera edge {feet.x - left:0.0} behind");

            static string Vec(Vector2 v) => $"({v.x:0.0}, {v.y:0.0})";
        }

        // Its strike still to come down ahead (the charges' 3-2-1, the crane's pallet): keep out of it.
        private bool StrikePending(float now) =>
            now < _strikeUntil && (FindAnyObjectByType<ImplosionCharges>() != null
                || FindAnyObjectByType<CraneCall>() != null || FindAnyObjectByType<CranePallet>() != null);

        // The charges a controlled demolition would set from here (the action's own pick), counting
        // only those on a standing block (with nothing ahead it sets one on the street in front),
        // and how near the nearest comes.
        private int Charges(Vector2 feet, out float nearest)
        {
            nearest = float.PositiveInfinity;
            int count = 0;
            foreach (Vector2 spot in ImplosionCharges.Pick(feet, 1))
            {
                nearest = Mathf.Min(nearest, Mathf.Abs(spot.x - feet.x));
                if (StandingAt(spot))
                    count++;
            }
            return count;
        }

        private static bool StandingAt(Vector2 point)
        {
            foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
            {
                var block = hit.GetComponentInParent<BuildingBlock>();
                if (block != null && !block.IsFalling && !block.IsRubble)
                    return true;
            }
            return false;
        }

        // Standing blocks in the column under the feet, as deep as the slam goes.
        private int FloorsBelow(Vector2 feet)
        {
            int floors = 0;
            int count = Physics2D.OverlapBox(feet + new Vector2(0f, -6.05f), new Vector2(0.6f, 12f), 0f, Any, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block != null && !block.IsFalling && !block.IsRubble)
                    floors++;
            }
            return floors;
        }

        // A street prop (crates, a car, a container) rather than a building, by the level's map.
        private bool IsProp(DestructibleBuilding building)
        {
            LevelLayout layout = _level != null ? _level.Layout : null;
            int index = building.NetIndex;
            return layout != null && index >= 0 && index < layout.Buildings.Count && layout.Buildings[index].IsProp;
        }

        // Health it can spend bringing a building down on itself (the floors over a support come
        // down on whoever punches it out): three quarters of the bar at the start, a quarter at the finish.
        private bool Daring(Vector2 feet)
        {
            if (_health == null || _health.Max <= 0f)
                return false;
            LevelLayout layout = _level != null ? _level.Layout : null;
            float progress = layout != null && layout.FinishX > layout.SpawnX
                ? Mathf.Clamp01((feet.x - _level.transform.position.x - layout.SpawnX) / (layout.FinishX - layout.SpawnX))
                : 0f;
            return _health.Current / _health.Max >= 0.25f + 0.5f * (1f - progress);
        }

        // Seconds it can stand still before the scrolling camera's left edge reaches it.
        private float Room(Vector2 feet)
        {
            float left = CameraLeft();
            if (float.IsNegativeInfinity(left))
                return float.PositiveInfinity;
            ScrollCamera scroll = RunController.Current.Camera.GetComponent<ScrollCamera>();
            float speed = scroll != null ? Mathf.Max(scroll.ScrollSpeed, 0.1f) : 2f;
            return (feet.x - left - EdgeMargin) / speed;
        }

        // The other player close by and swinging at something: worth the shovel lean's boost.
        private bool PartnerWorking(Vector2 feet)
        {
            RunController run = RunController.Current;
            if (run == null)
                return false;
            foreach (RunController.Player p in run.Players)
                if (p.Transform != transform && !p.Health.IsDead && p.Input.AttackHeld
                    && Mathf.Abs(p.Transform.position.x - feet.x) < 6f)
                    return true;
            return false;
        }

        // A stick still to go off this near (its own: the partner's would be as bad).
        private bool StickNear(Vector2 feet)
        {
            foreach (Dynamite stick in FindObjectsByType<Dynamite>(FindObjectsSortMode.None))
                if (!stick.Cosmetic && Mathf.Abs(stick.transform.position.x - feet.x) < StickClear)
                    return true;
            return false;
        }

        // A support keeps a quarter after one stick: a second finishes it from outside, fists
        // would bring the floors down on the puncher.
        private static bool WorthAStick(BuildingBlock block) =>
            block.Explosive || (block.HealthFraction > 0.15f
                && (block.Material == BlockMaterial.Support || block.Material == BlockMaterial.Metal));

        // A standing block at body height just in front.
        private static BuildingBlock BlockAhead(Vector2 feet)
        {
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(feet + new Vector2(1.1f, 1.1f), new Vector2(0.8f, 1.6f), 0f))
            {
                var block = hit.GetComponentInParent<BuildingBlock>();
                if (block != null && !block.IsFalling && !block.IsRubble)
                    return block;
            }
            return null;
        }

        // A gas tank coming up at the feet: jumped before the swing would reach it.
        private bool ExplosiveAhead(Vector2 feet)
        {
            int count = Physics2D.OverlapBox(feet + new Vector2(1.9f, 0.6f), new Vector2(1.6f, 1f), 0f, Any, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block != null && block.Explosive && !block.IsFalling)
                    return true;
            }
            return false;
        }

        // Blocks coming down on the player's head (a collapse it set off), and the way out from
        // under them (-1 back, +1 on only when nothing falls ahead).
        private bool DebrisAbove(Vector2 feet, out float escape)
        {
            escape = -1f;
            bool above = false;
            float left = float.MaxValue, right = float.MinValue;
            int count = Physics2D.OverlapBox(feet + new Vector2(0f, 6.5f), new Vector2(12f, 11f), 0f, Solid, _overlaps);
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D body = _overlaps[i].attachedRigidbody;
                if (body == null || body.linearVelocity.y > -1f)
                    continue;
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block == null || !block.IsFalling)
                    continue;
                float x = body.position.x;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                above |= x > feet.x - 1.1f && x < feet.x + 2.3f;
            }
            if (above && right < feet.x + 0.5f)
                escape = 1f; // on only when all of it is behind the body: what's ahead comes down in its way
            return above;
        }

        // Rubble against the front of the body: at chest height (a heap too tall to step onto), or
        // from the ground up when there's no room to jump it. A loose block that has stopped
        // moving counts; one still tumbling is waited out (DebrisAhead).
        private bool HeapAhead(Vector2 feet, bool fromGround)
        {
            float bottom = fromGround ? 0.15f : 0.9f;
            int count = Physics2D.OverlapBox(feet + new Vector2(0.8f, (bottom + 1.7f) * 0.5f), new Vector2(0.9f, 1.7f - bottom), 0f, Solid, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block != null && !block.Explosive && (block.IsRubble || (block.IsFalling && !Tumbling(_overlaps[i]))))
                    return true;
            }
            return false;
        }

        // Loose blocks still tumbling just in front, at body height.
        private bool DebrisAhead(Vector2 feet)
        {
            int count = Physics2D.OverlapBox(feet + new Vector2(1.3f, 1.2f), new Vector2(1.8f, 2.2f), 0f, Solid, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var block = _overlaps[i].GetComponentInParent<BuildingBlock>();
                if (block != null && block.IsFalling && Tumbling(_overlaps[i]))
                    return true;
            }
            return false;
        }

        private static bool Tumbling(Collider2D debris) =>
            debris.attachedRigidbody != null && debris.attachedRigidbody.linearVelocity.sqrMagnitude > 0.25f;

        // Pressing against the motion until it stands (letting go coasts 7 units from full speed), then standing.
        private float Brake(float standing)
        {
            float vx = _body != null ? _body.linearVelocity.x : 0f;
            return vx > BotTactics.StandStill ? -1f : vx < -BotTactics.StandStill ? 1f : standing;
        }

        // -1 to back up, or a stop where that would end over a drop or past the camera's edge.
        private float Back(Vector2 feet) =>
            feet.x - 1f < CameraLeft() + EdgeMargin || PitAt(feet + new Vector2(-0.7f, 0.2f)) || PitAt(feet + new Vector2(-1.6f, 0.2f))
                ? Brake(0f) : -1f;

        // The run camera's left edge (out of bounds past it), or -infinity when nothing scrolls.
        private static float CameraLeft()
        {
            RunController run = RunController.Current;
            if (run == null || run.IgnoreBounds || run.Camera == null)
                return float.NegativeInfinity;
            return run.Camera.ViewportToWorldPoint(Vector3.zero).x;
        }

        // Solid ground rising in front of the feet (not a standing building: those get hit;
        // rubble counts, it gets climbed).
        private bool StepAhead(Vector2 feet)
        {
            int count = Physics2D.Raycast(feet + new Vector2(0.9f, 0.3f), Vector2.right, Solid, _hits, 0.6f);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i].collider;
                var block = hit.GetComponentInParent<BuildingBlock>();
                if ((block == null || block.IsRubble) && hit.GetComponentInParent<PlayerHealth>() == null)
                    return true;
            }
            return false;
        }

        // Room over the head for a jump: no block just above it, from the back of the body (whose top
        // is 1.77 up, 0.44 behind the feet) to a little ahead; a block coming down counts too. The
        // ground doesn't: it is a height map, never overhead, so what it puts there is the top of
        // a taller step in front, the very thing to jump onto.
        private bool Headroom(Vector2 feet)
        {
            int count = Physics2D.OverlapBox(feet + new Vector2(0.25f, 2.6f), new Vector2(1.4f, 1.6f), 0f, Solid, _overlaps);
            for (int i = 0; i < count; i++)
                if (_overlaps[i].GetComponentInParent<BuildingBlock>() != null)
                    return false;
            return true;
        }

        private bool PitAhead(Vector2 feet) => PitAt(feet + new Vector2(1.2f, 0.2f));

        // A running jump lands up to 10 units on (9.5 at full speed, less from a standstill): a
        // step or heap isn't jumped with a pit anywhere there (a pit at the feet is, the level is
        // made to be cleared from its edge).
        private bool LandingClear(Vector2 feet)
        {
            for (float ahead = 4f; ahead <= 10.5f; ahead += 0.5f)
                if (PitAt(feet + new Vector2(ahead, 0.2f)))
                    return false;
            return true;
        }

        // No ground under the point: the level's map where there is one (a heap of rubble or a block
        // dropping through a pit is no ground to land on), else a ray down past falling debris.
        private bool PitAt(Vector2 point)
        {
            LevelLayout layout = _level != null ? _level.Layout : null;
            int column = layout != null ? Mathf.FloorToInt(point.x - _level.transform.position.x) : -1;
            if (column >= 0 && column < layout.Width)
                return layout.GroundAt(column) == LevelLayout.NoGround;
            int count = Physics2D.Raycast(point, Vector2.down, Solid, _hits, 4f);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i].collider;
                var block = hit.GetComponentInParent<BuildingBlock>();
                if ((block == null || !block.IsFalling) && hit.GetComponentInParent<PlayerHealth>() == null)
                    return false;
            }
            return true;
        }
    }
}
