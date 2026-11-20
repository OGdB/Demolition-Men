using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Movement with the exact feel of the original PlayerMovement: an impulse of
    /// input × (MaxSpeed / 2) every physics tick while below MaxSpeed (mass 3 + linear
    /// damping 1 give the old ramp-up and coast), a jump impulse with heavier
    /// "better-jump" gravity on the way down, and the same animator contract
    /// (Speed float, InAir bool, sprite flipX). On a ladder (<see cref="Generation.Ladder"/>)
    /// holding up takes hold of it: no gravity, up and down by the stick on the ladder's centre
    /// line, off by jumping, at its foot, or sideways once the feet are at its top. The art has
    /// no climbing clip: the run plays.
    ///
    /// Works with or without an Animator/SpriteRenderer/AudioSource, so the code-built
    /// demo player and the sprite prefabs share this one script.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Running")]
        [SerializeField] private float maxSpeed = 8f;
        [Tooltip("Share of max speed while the attack button is held: swings plant the "
               + "player instead of sliding them along at full run speed.")]
        [SerializeField, Range(0f, 1f)] private float attackMoveFactor = 0.35f;

        [Header("Jumping")]
        [SerializeField] private float jumpImpulse = 45f;
        [SerializeField] private float fallMultiplier = 3.8f;
        [SerializeField] private float lowJumpMultiplier = 3f;

        [Header("Climbing")]
        [SerializeField] private float climbSpeed = 5f;

        [Header("Ground check")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioClip glassBreakSound;

        public bool IsGrounded { get; private set; }
        /// <summary>Holding on to a ladder.</summary>
        public bool IsClimbing { get; private set; }
        /// <summary>The ladder in reach, or null.</summary>
        public Generation.Ladder Ladder { get; private set; }
        public float MaxSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0f, value); }
        public float JumpImpulse => jumpImpulse;
        /// <summary>+1 facing right, -1 facing left (same convention as directionCalc).</summary>
        public int Facing { get; private set; } = 1;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InAirParam = Animator.StringToHash("InAir");
        /// <summary>Slower than this (units/s) the legs don't run, whatever the stick says: pushing
        /// against a wall or rubble, or a nudge that only turns the player (a bot's facing).</summary>
        private const float RunsFrom = 0.5f;

        private PlayerInputReader _input;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Animator _anim;
        private AudioSource _audio;
        private bool _jumpQueued;
        private bool _launched; // jumped or thrown, and still rising
        private float _staggerUntil;
        private ContactFilter2D _groundFilter;
        private float _gravityScale;
        private int _ladderTouches; // the player has two colliders: one leaving the ladder isn't both

        /// <summary>The stick this far up takes hold of a ladder: a run with the stick a little up doesn't.</summary>
        private const float GrabAt = 0.6f;
        /// <summary>How hard a climber is drawn to the ladder's centre line (speed per unit off it).</summary>
        private const float CentreGain = 10f;

        /// <summary>Seconds a knockback flies uncontrolled before running and swinging take over again.</summary>
        private const float StaggerSeconds = 0.35f;

        /// <summary>Thrown by a blast: replaces the velocity (running into it doesn't cancel the
        /// shove) and runs uncontrolled for a moment.</summary>
        public void Knockback(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
            _staggerUntil = Time.time + StaggerSeconds;
            LeaveGround();
        }

        /// <summary>A flinch upwards (a trap): rising at least this fast.</summary>
        public void Hop(float speed)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Max(_rb.linearVelocity.y, speed));
            LeaveGround();
        }

        private bool Staggered => Time.time < _staggerUntil;

        // Contacts are a physics step old: right after a launch they still report the ground the
        // player just left, and a jump in that step would stack on the launch (a jump on a jump
        // goes four times as high). Rising along a wall of stacked blocks, the feet also touch
        // every seam. So the player is off the ground until they stop rising.
        private void LeaveGround()
        {
            _launched = true;
            IsGrounded = false;
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _rb = GetComponent<Rigidbody2D>();
            // Better-jump falls land at ~0.5 units per step: discrete sinks the player into a
            // seam between two block colliders, both push sideways, and they're wedged.
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _sr = GetComponentInChildren<SpriteRenderer>();
            _anim = GetComponent<Animator>();
            _audio = GetComponent<AudioSource>();
            // Grounded = touching something whose contact normal points up (within 45°),
            // so a wall beside the feet can never count and walls can't be jumped up.
            _groundFilter.SetLayerMask(groundMask);
            _groundFilter.SetNormalAngle(45f, 135f);
        }

        // Jump input is buffered every frame and consumed by physics in FixedUpdate —
        // the same Update/FixedUpdate split (and queue/cancel rules) as the original.
        private void Update()
        {
            // On a keyboard up is the jump key: in reach of a ladder that press is the climb.
            bool grabbing = Ladder != null && _input.MoveRaw.y > GrabAt;
            if (_input.JumpDown && (IsGrounded || IsClimbing) && !grabbing)
                _jumpQueued = true;
            else if (_input.JumpUp)
                _jumpQueued = false;
        }

        private void FixedUpdate()
        {
            GroundCheck();
            Climb();
            Run();
            Jump();
        }

        private void Climb()
        {
            float y = _input.MoveRaw.y;
            bool hold = IsClimbing;
            if (Ladder == null || Staggered || _input.Suppressed) // an action's frames have the controls
                hold = false;
            else if (!hold)
                hold = y > GrabAt && _rb.position.y < Ladder.Top - 0.05f;
            else if (IsGrounded && y < -GrabAt)
                hold = false; // down at its foot
            Hold(hold);
            if (!IsClimbing)
                return;
            // The feet stop at the floor the ladder leads to: step off sideways from there. Below
            // it the climber is drawn to the ladder's middle, clear of the wall beside it: pressed
            // against a wall of stacked blocks, a corner of the body catches on a seam and hangs.
            float up = y > 0f && _rb.position.y >= Ladder.Top ? 0f : y * climbSpeed;
            float across = OnRungs
                ? Mathf.Clamp((Ladder.transform.position.x - _rb.position.x) * CentreGain, -climbSpeed, climbSpeed)
                : _rb.linearVelocity.x;
            _rb.linearVelocity = new Vector2(across, up);
        }

        /// <summary>Climbing with the feet below the ladder's top: sideways input waits.</summary>
        private bool OnRungs => IsClimbing && Ladder != null && _rb.position.y < Ladder.Top - 0.05f;

        private void Hold(bool hold)
        {
            if (hold == IsClimbing)
                return;
            IsClimbing = hold;
            if (hold)
                _gravityScale = _rb.gravityScale;
            _rb.gravityScale = hold ? 0f : _gravityScale;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent(out Generation.Ladder ladder))
                return;
            if (Ladder != ladder)
                _ladderTouches = 0;
            Ladder = ladder;
            _ladderTouches++;
        }

        // Also sent when the ladder is destroyed under the player.
        private void OnTriggerExit2D(Collider2D other)
        {
            if (Ladder != null && other.gameObject == Ladder.gameObject && --_ladderTouches <= 0)
                Ladder = null;
        }

        private void Run()
        {
            if (Staggered)
            {
                Animate(0f, 0f); // thrown: no running, and the legs don't keep the last input's run
                return;
            }
            // What the last physics step moved: an impulse shows in the velocity at once, so read
            // after it a player pushing against a wall would seem to run.
            float moving = Mathf.Abs(_rb.linearVelocity.x);
            float x = OnRungs ? 0f : _input.MoveRaw.x;
            bool attacking = _input.AttackHeld;
            float cap = attacking ? maxSpeed * attackMoveFactor : maxSpeed;

            if (Mathf.Abs(_rb.linearVelocity.x) <= cap && Mathf.Abs(x) > 0.2f)
                _rb.AddForce(new Vector2(x * (cap * 0.5f), 0f), ForceMode2D.Impulse);
            if (attacking && Mathf.Abs(_rb.linearVelocity.x) > cap)
                _rb.linearVelocity = new Vector2(Mathf.Sign(_rb.linearVelocity.x) * cap, _rb.linearVelocity.y);

            // Facing is locked mid-swing, so a swing lands where it started.
            float smoothedX = _input.MoveSmoothed.x;
            if (!attacking && smoothedX < 0f) Facing = -1;
            else if (!attacking && smoothedX > 0f) Facing = 1;
            if (_sr != null)
                _sr.flipX = Facing < 0;

            Animate(smoothedX, moving);
        }

        // The run cycle plays while the player runs: the stick's push, but only once the body moves.
        private void Animate(float smoothedX, float moving)
        {
            if (_anim == null)
                return;
            float run = moving < RunsFrom ? 0f : Mathf.Abs(smoothedX);
            _anim.SetFloat(SpeedParam, IsClimbing ? Mathf.Max(Mathf.Abs(smoothedX), Mathf.Abs(_input.MoveRaw.y)) : run);
        }

        private void Jump()
        {
            if (_jumpQueued && (IsGrounded || IsClimbing))
            {
                Hold(false);
                _jumpQueued = false;
                PlayClip(jumpSound);
                _rb.AddForce(new Vector2(0f, jumpImpulse), ForceMode2D.Impulse);
                LeaveGround();
            }

            if (IsClimbing)
                return; // the ladder holds them: no gravity to shape

            // "Better jump" gravity shaping, unchanged — including the original quirk
            // that the short-hop cut only applies without horizontal input, so running
            // jumps always reach full height.
            if (_rb.linearVelocity.y < 0f)
            {
                _jumpQueued = false;
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
            }
            else if (_rb.linearVelocity.y > 0f && !_input.JumpHeld && Mathf.Abs(_input.MoveRaw.x) < 0.2f && !Staggered)
            {
                _jumpQueued = false;
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private void GroundCheck()
        {
            if (_launched && _rb.linearVelocity.y <= 0f)
                _launched = false;
            IsGrounded = !_launched && _rb.IsTouching(_groundFilter);
            if (_anim != null)
                _anim.SetBool(InAirParam, !IsGrounded && !IsClimbing);
        }

        // Original behaviour: glass shatters the moment the player touches it — walking
        // into a window breaks it (and a falling pane breaks on the player's head).
        private void OnCollisionEnter2D(Collision2D collision)
        {
            var block = collision.collider.GetComponentInParent<BuildingBlock>();
            if (block != null && block.Material == BlockMaterial.Glass)
            {
                PlayClip(glassBreakSound);
                block.Shatter(_input.PlayerIndex, Ranked.HitCause.Glass);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
        }
    }
}
