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

        [Header("Jumping")]
        [SerializeField] private float jumpImpulse = 45f;
        [SerializeField] private float fallMultiplier = 3.8f;
        [SerializeField] private float lowJumpMultiplier = 3f;

        [Header("Ground check")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        [Tooltip("Ground box centre relative to the transform (the prefabs pivot at the feet).")]
        [SerializeField] private Vector2 groundCheckOffset = Vector2.zero;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioClip glassBreakSound;

        public bool IsGrounded { get; private set; }
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

        public void SetGroundCheckOffset(Vector2 offset) => groundCheckOffset = offset;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _anim = GetComponent<Animator>();
            _audio = GetComponent<AudioSource>();
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
            float x = _input.MoveRaw.x;

            if (Mathf.Abs(_rb.linearVelocity.x) <= maxSpeed && Mathf.Abs(x) > 0.2f)
                _rb.AddForce(new Vector2(x * (maxSpeed * 0.5f), 0f), ForceMode2D.Impulse);

            float smoothedX = _input.MoveSmoothed.x;
            if (smoothedX < 0f) Facing = -1;
            else if (smoothedX > 0f) Facing = 1;
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
            else if (_rb.linearVelocity.y > 0f && !_input.JumpHeld && Mathf.Abs(_input.MoveRaw.x) < 0.2f)
            {
                _jumpQueued = false;
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private void GroundCheck()
        {
            Vector2 centre = (Vector2)transform.position + groundCheckOffset;
            var hits = Physics2D.OverlapBoxAll(centre, groundCheckSize, 0f, groundMask);

            IsGrounded = false;
            foreach (var hit in hits)
            {
                if (hit == null || hit.isTrigger || hit.attachedRigidbody == _rb)
                    continue;
                IsGrounded = true;
                break;
            }

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
                block.Shatter();
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
        }

        // Green when grounded, red when airborne (grey outline in edit mode).
        private void OnDrawGizmos()
        {
            Vector3 centre = transform.position + (Vector3)groundCheckOffset;

            Color colour = !Application.isPlaying ? Color.grey
                : IsGrounded ? Color.green
                : Color.red;

            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.25f);
            Gizmos.DrawCube(centre, groundCheckSize);
            Gizmos.color = colour;
            Gizmos.DrawWireCube(centre, groundCheckSize);
        }
    }
}
