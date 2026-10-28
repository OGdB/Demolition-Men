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
    /// collapse; and never so far back that the scrolling camera leaves it behind. Walking to a
    /// point (<see cref="StopX"/>, the Clip Lab) it only clears the way. Drives the player
    /// through a <see cref="ScriptedInputSource"/>, exactly like a real input device.
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
        private static readonly float[] ThrowHeights = { 0.5f, 1.3f };

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
        /// <summary>Fires its action as soon as the meter is full and a block is ahead (the ranked tests).</summary>
        public bool UsesActions { get; set; }
        public bool Arrived { get; private set; }
        /// <summary>What it decided this frame, for watching it from tools.</summary>
        public string Status { get; private set; } = "";

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
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
            if (feet.x + Reach >= StopX || over)
            {
                // Arrived, or the run is over: hands off (on the results screen it would punch the air).
                Arrived |= feet.x + Reach >= StopX;
                Status = over ? "run over" : "arrived";
                _source.Next = new PlayerInputFrame { JumpReleased = _jumpHeld };
                _jumpHeld = false;
                return;
            }

            BuildingBlock block = BlockAhead(feet);
            bool headroom = Headroom(feet);
            // A heap in the way gets punched through as a player would, rather than walked into:
            // a step of it is jumped where there's room overhead (under a floor there isn't).
            bool attack = block != null || HeapAhead(feet, !headroom);
            bool throwNow = false;
            float move = 1f;
            if (float.IsPositiveInfinity(StopX))
            {
                if (block != null && block.Explosive)
                    attack = false; // the punch that breaks it goes off in our face: jumped, or blown up from afar
                throwNow = PlanThrow(feet, now, ref move);
                if (DebrisAbove(feet, out float escape))
                {
                    _backUntil = now + 0.35f;
                    // Forward only if the way is open, and never towards a pit's edge.
                    bool open = block == null && !PitAt(feet + new Vector2(1.2f, 0.2f)) && !PitAt(feet + new Vector2(3f, 0.2f));
                    _escape = escape > 0f && !open ? -1f : escape;
                }
                if (now < _backUntil)
                    move = _escape;
                else if (move > 0f && DebrisAhead(feet))
                    move = 0f; // tumbling blocks ahead may yet come down on its head
                else if (_combat.DynamiteOut)
                    move = Mathf.Min(move, 0f); // ours is about to go off ahead: thrown from out of its reach, so stay there
                if (move < 0f)
                {
                    attack = false;
                    move = Back(feet);
                }
            }

            // Standing back on purpose isn't being stuck.
            if (move <= 0f || feet.x > _progressX + 0.5f)
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
            if (pit && !headroom && move > 0f && PitAt(feet + new Vector2(0.45f, 0.2f)))
                move = 0f;
            if (CanArmJump && grounded && move > 0f && !attack && headroom
                && (StepAhead(feet) || pit || ExplosiveAhead(feet)))
                _jumpUntil = now + JumpHold;
            else if (move > 0f && grounded && CanArmJump && (unstickJump || (headroom && now - _progressTime > StuckSeconds)))
            {
                _jumpUntil = now + JumpHold;
                _progressTime = now;
            }
            bool jump = now < _jumpUntil;
            Status = $"move {move:0} attack {attack} jump {jump} throw {throwNow} head {headroom} pit {pit} gnd {grounded} ahead {(block != null ? $"{block.Material}{(block.Explosive ? "!" : "")} {block.HealthFraction:0.00}" : "-")}"
                     + $" back {Mathf.Max(0f, _backUntil - now):0.0} runUp {(float.IsNegativeInfinity(_runUpFrom) ? "-" : $"{_runUpFrom:0.0}")} out {_combat.DynamiteOut} tnt {_combat.DynamiteCount}";

            _source.Next.Move = new Vector2(move, 0f);
            _source.Next.AttackHeld = attack;
            _source.Next.DynamitePressed = throwNow;
            _source.Next.ActionPressed = UsesActions && block != null && TryGetComponent(out Actions.ActionUser action)
                && action.Meter != null && action.Meter.Full;
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
            if (!ThrowsDynamite || _combat.DynamiteCount == 0 || _combat.DynamiteOut)
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

        // The nearest standing block straight ahead at waist or chest height, if it's worth a stick.
        private BuildingBlock ThrowTarget(Vector2 feet, out float distance)
        {
            distance = float.MaxValue;
            BuildingBlock nearest = null;
            foreach (float height in ThrowHeights)
            {
                int count = Physics2D.Raycast(feet + new Vector2(0.3f, height), Vector2.right, Any, _hits, ThrowMax + 1f);
                for (int i = 0; i < count; i++)
                {
                    Collider2D hit = _hits[i].collider;
                    if (hit.GetComponentInParent<PlayerHealth>() != null)
                        continue;
                    var block = hit.GetComponentInParent<BuildingBlock>();
                    if (block == null && hit.isTrigger)
                        continue; // pickups, the finish line
                    if (block != null && block.IsFalling)
                        continue;
                    // The first solid thing in the way: the stick goes off on it (or stops at a wall of ground).
                    if (block != null && !block.IsRubble && _hits[i].distance + 0.3f < distance)
                    {
                        distance = _hits[i].distance + 0.3f;
                        nearest = block;
                    }
                    break;
                }
            }
            return nearest != null && WorthAStick(nearest) ? nearest : null;
        }

        private static bool WorthAStick(BuildingBlock block) =>
            block.Explosive || (block.HealthFraction > 0.35f
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

        // Blocks coming down on the player's head (a collapse it set off), and the shorter way
        // out from under them (-1 back, +1 on; back when it's about even: ahead is still standing).
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
            if (above && feet.x - left > right - feet.x + 2f)
                escape = 1f;
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

        // -1 to back up, or 0 where that would end over a drop or past the camera's edge (backing up
        // carries on a little after letting go).
        private float Back(Vector2 feet) =>
            feet.x - 1f < CameraLeft() + EdgeMargin || PitAt(feet + new Vector2(-0.7f, 0.2f)) || PitAt(feet + new Vector2(-1.6f, 0.2f))
                ? 0f : -1f;

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
