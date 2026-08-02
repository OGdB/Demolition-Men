using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// One destructible cell in a <see cref="DestructibleBuilding"/>.
    ///
    /// Option C lifecycle:
    ///  - <b>Supported</b>: Rigidbody2D is Static (near-zero solver cost, no stacking jitter).
    ///  - <b>Falling</b>: on detach it flips to Dynamic and can hurt the player on impact.
    ///  - <b>Rubble</b>: once it settles it re-freezes to Static and stays around as debris,
    ///    so it costs nothing yet remains visible.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BuildingBlock : MonoBehaviour
    {
        [SerializeField] private BlockMaterial material = BlockMaterial.Brick;
        [SerializeField] private float maxHealth = -1f; // <0 => use material default

        [Header("Collapse")]
        [SerializeField] private float detachImpulse = 0.4f;
        [SerializeField] private float settleLinearThreshold = 0.25f;
        [SerializeField] private float settleAngularThreshold = 12f;
        [SerializeField] private float settleTime = 0.5f;

        [Header("Impact damage to player")]
        [SerializeField] private float minImpactSpeed = 3.5f;
        [SerializeField] private float impactDamageScale = 6f;
        [SerializeField] private float maxImpactDamage = 45f;
        [SerializeField] private float impactCooldown = 0.4f;

        public BlockMaterial Material => material;
        public Vector2Int Coord { get; private set; }
        public bool IsFalling { get; private set; }
        public bool IsRubble { get; private set; }

        private DestructibleBuilding _building;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private float _health;
        private float _resolvedMaxHealth;
        private float _settleTimer;
        private float _lastImpactTime = -999f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Static;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        public void Initialise(DestructibleBuilding building, Vector2Int coord, BlockMaterial mat)
        {
            _building = building;
            Coord = coord;
            material = mat;
            _resolvedMaxHealth = maxHealth > 0f ? maxHealth : BlockMaterialInfo.DefaultHealth(mat);
            _health = _resolvedMaxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
                return;

            _health -= amount;

            // Chips of debris + progressive darkening as the block weakens.
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(material), 5, 3f, 0.1f, 0.4f);
            if (_sr != null)
            {
                float frac = Mathf.Clamp01(_health / _resolvedMaxHealth);
                _sr.color = _baseColor * (0.45f + 0.55f * frac);
            }

            if (_health <= 0f)
                Destroyed();
        }

        private void Destroyed()
        {
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(material), 16, 5f, 0.16f, 0.75f);
            if (_building != null)
                _building.OnBlockDestroyed(this);
            Destroy(gameObject);
        }

        /// <summary>Static structure -> falling debris. Called for every detached cell.</summary>
        public void BeginFalling()
        {
            if (IsFalling || IsRubble)
                return;
            IsFalling = true;
            _settleTimer = 0f;

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 1f;
            _rb.AddForce(new Vector2((Random.value - 0.5f) * detachImpulse, 0f), ForceMode2D.Impulse);
            _rb.AddTorque((Random.value - 0.5f) * detachImpulse);
        }

        private void FixedUpdate()
        {
            if (!IsFalling)
                return;

            // Settle detection: once it has come to rest, freeze it into persistent rubble.
            bool slow = _rb.linearVelocity.magnitude < settleLinearThreshold
                        && Mathf.Abs(_rb.angularVelocity) < settleAngularThreshold;
            _settleTimer = slow ? _settleTimer + Time.fixedDeltaTime : 0f;

            if (_settleTimer >= settleTime)
                BecomeRubble();
        }

        private void BecomeRubble()
        {
            IsFalling = false;
            IsRubble = true;
            _rb.bodyType = RigidbodyType2D.Static; // leaves the solver; stays visible as debris
            if (_sr != null)
                _sr.color = _baseColor * 0.7f; // read as rubble
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsFalling)
                return;

            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health == null)
                return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < minImpactSpeed || Time.time - _lastImpactTime < impactCooldown)
                return;

            _lastImpactTime = Time.time;
            float damage = Mathf.Clamp((impact - minImpactSpeed) * impactDamageScale, 0f, maxImpactDamage);
            health.TakeDamage(damage);
            Particles.Burst(collision.GetContact(0).point, new Color(0.95f, 0.8f, 0.2f), 8, 4f, 0.12f, 0.5f);
        }
    }
}
