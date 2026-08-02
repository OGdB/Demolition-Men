using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Minimal, self-contained "Demolition Man" controller for testing the destruction
    /// system. Uses the legacy Input API (keyboard) so it needs no gamepad,
    /// ControllerManager, or Input System asset.
    ///
    /// Controls: A/D or ◀/▶ move · Space or W jump · J or Left-Mouse punch.
    ///
    /// The punch overlap area is always visible as a faint ring in front of the player
    /// (dimmed while the punch is on cooldown), and every swing flashes the actual
    /// overlap circle.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpVelocity = 11f;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.7f, 0.12f);

        [Header("Punch")]
        [SerializeField] private float punchRange = 0.9f;
        [SerializeField] private float punchRadius = 0.55f;
        [SerializeField] private float punchDamage = 40f;
        [SerializeField] private float punchCooldown = 0.25f;
        [SerializeField] private float punchKnockback = 3f;

        [Header("Feedback")]
        [SerializeField] private float markerAlpha = 0.25f;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private int _facing = 1;
        private float _lastPunchTime = -999f;
        private bool _grounded;
        private Transform _marker;
        private SpriteRenderer _markerSr;

        // Reused buffer so punching allocates nothing.
        private readonly Collider2D[] _punchHits = new Collider2D[16];

        /// <summary>World-space centre of the punch overlap circle.</summary>
        private Vector2 PunchOrigin =>
            (Vector2)transform.position + Vector2.right * (_facing * punchRange);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _rb.freezeRotation = true;
            CreateRangeMarker();
        }

        // Unparented on purpose: the player root has a non-uniform scale, which would
        // squash a child ring into an ellipse. LateUpdate keeps it glued to the fist.
        private void CreateRangeMarker()
        {
            var go = new GameObject("PunchRangeMarker");
            _markerSr = go.AddComponent<SpriteRenderer>();
            _markerSr.sprite = PrimitiveSprite.Ring();
            _markerSr.color = new Color(1f, 1f, 1f, markerAlpha);
            _markerSr.sortingOrder = 60;
            _marker = go.transform;
            _marker.localScale = Vector3.one * (punchRadius * 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
                TryPunch();
        }

        private void LateUpdate()
        {
            if (_marker == null)
                return;
            _marker.position = PunchOrigin;

            // Dim the ring while the punch is recharging so timing reads at a glance.
            bool ready = Time.time - _lastPunchTime >= punchCooldown;
            var c = _markerSr.color;
            c.a = ready ? markerAlpha : markerAlpha * 0.35f;
            _markerSr.color = c;
        }

        private void OnDestroy()
        {
            if (_marker != null)
                Destroy(_marker.gameObject);
        }

        private void FixedUpdate()
        {
            _grounded = CheckGrounded();

            float x = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;

            if (Mathf.Abs(x) > 0.01f)
                _facing = x > 0f ? 1 : -1;

            _rb.linearVelocity = new Vector2(x * moveSpeed, _rb.linearVelocity.y);

            bool jump = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            if (jump && _grounded)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
        }

        private bool CheckGrounded()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.down * (_col.bounds.extents.y + groundCheckDistance);
            var hits = Physics2D.OverlapBoxAll(origin, groundCheckSize, 0f);
            foreach (var h in hits)
            {
                if (h != null && h != _col)
                    return true;
            }
            return false;
        }

        private void TryPunch()
        {
            if (Time.time - _lastPunchTime < punchCooldown)
                return;
            _lastPunchTime = Time.time;

            Vector2 origin = PunchOrigin;
            SpawnSwingFlash(origin);

            int count = Physics2D.OverlapCircleNonAlloc(origin, punchRadius, _punchHits);
            for (int i = 0; i < count; i++)
            {
                var hit = _punchHits[i];
                if (hit == null)
                    continue;
                var block = hit.GetComponentInParent<BuildingBlock>();
                if (block == null)
                    continue;

                block.TakeDamage(punchDamage);

                // Only shoves things that are already loose (falling debris); a
                // supported static wall just takes damage — correct Option C behaviour.
                if (block.IsFalling)
                {
                    var brb = block.GetComponent<Rigidbody2D>();
                    if (brb != null)
                        brb.AddForce(new Vector2(_facing * punchKnockback, punchKnockback * 0.35f), ForceMode2D.Impulse);
                }
            }
        }

        private void SpawnSwingFlash(Vector2 origin)
        {
            var go = new GameObject("PunchFlash");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Disc();
            sr.color = new Color(1f, 0.85f, 0.35f, 0.5f);
            sr.sortingOrder = 61;
            go.AddComponent<SpriteFlash>().Configure(punchRadius * 2f, 0.13f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector2 origin = (Vector2)transform.position + Vector2.right * (_facing * punchRange);
            Gizmos.DrawWireSphere(origin, punchRadius);
        }
    }
}
