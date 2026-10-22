using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Movement with the exact feel of the original PlayerMovement: an impulse of
    /// input × (MaxSpeed / 2) every physics tick while below MaxSpeed (mass 3 + linear
    /// damping 1 give the old ramp-up and coast), a jump impulse with heavier
    /// "better-jump" gravity on the way down, and the same animator contract
    /// (Speed float, InAir bool, sprite flipX).
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

        [Header("Ground check")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioClip glassBreakSound;

        public bool IsGrounded { get; private set; }
        public float MaxSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0f, value); }
        public float JumpImpulse => jumpImpulse;
        /// <summary>+1 facing right, -1 facing left (same convention as directionCalc).</summary>
        public int Facing { get; private set; } = 1;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InAirParam = Animator.StringToHash("InAir");

        private PlayerInputReader _input;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Animator _anim;
        private AudioSource _audio;
        private bool _jumpQueued;
        private float _staggerUntil;
        private ContactFilter2D _groundFilter;

        /// <summary>Seconds a knockback flies uncontrolled before running and swinging take over again.</summary>
        private const float StaggerSeconds = 0.35f;

        /// <summary>Thrown by a blast: replaces the velocity (running into it doesn't cancel the
        /// shove) and runs uncontrolled for a moment.</summary>
        public void Knockback(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
            _staggerUntil = Time.time + StaggerSeconds;
        }

        private bool Staggered => Time.time < _staggerUntil;

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
            if (_input.JumpDown && IsGrounded)
                _jumpQueued = true;
            else if (_input.JumpUp)
                _jumpQueued = false;
        }

        private void FixedUpdate()
        {
            GroundCheck();
            Run();
            Jump();
        }

        private void Run()
        {
            if (Staggered)
                return;
            float x = _input.MoveRaw.x;
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

            if (_anim != null)
                _anim.SetFloat(SpeedParam, Mathf.Abs(smoothedX));
        }

        private void Jump()
        {
            if (_jumpQueued && IsGrounded)
            {
                _jumpQueued = false;
                PlayClip(jumpSound);
                _rb.AddForce(new Vector2(0f, jumpImpulse), ForceMode2D.Impulse);
            }

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
            IsGrounded = _rb.IsTouching(_groundFilter);
            if (_anim != null)
                _anim.SetBool(InAirParam, !IsGrounded);
        }

        // Original behaviour: glass shatters the moment the player touches it — walking
        // into a window breaks it (and a falling pane breaks on the player's head).
        private void OnCollisionEnter2D(Collision2D collision)
        {
            var block = collision.collider.GetComponentInParent<BuildingBlock>();
            if (block != null && block.Material == BlockMaterial.Glass)
            {
                PlayClip(glassBreakSound);
                block.Shatter(_input.PlayerIndex);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
        }
    }
}
