using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Simple health for the test player. Falling blocks deal impact damage
    /// (see <see cref="BuildingBlock"/>). On death the player respawns at its start
    /// position so the bench stays usable.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float respawnInvulnerability = 1.5f;

        public float Max => maxHealth;
        public float Current { get; private set; }

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private Vector3 _spawnPoint;
        private float _invulnUntil;
        private float _flashUntil;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
            Current = maxHealth;
        }

        private void Start()
        {
            _spawnPoint = transform.position;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || Time.time < _invulnUntil)
                return;

            Current = Mathf.Max(0f, Current - amount);
            _flashUntil = Time.time + 0.12f;
            Particles.Burst(transform.position, new Color(0.9f, 0.2f, 0.2f), 10, 3.5f, 0.12f, 0.5f);

            if (Current <= 0f)
                Respawn();
        }

        private void Respawn()
        {
            transform.position = _spawnPoint;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
            Current = maxHealth;
            _invulnUntil = Time.time + respawnInvulnerability;
        }

        private void Update()
        {
            if (_sr == null)
                return;
            bool invuln = Time.time < _invulnUntil;
            if (Time.time < _flashUntil)
                _sr.color = Color.red;
            else
                _sr.color = invuln ? _baseColor * new Color(1f, 1f, 1f, 0.5f) : _baseColor;
        }
    }
}
