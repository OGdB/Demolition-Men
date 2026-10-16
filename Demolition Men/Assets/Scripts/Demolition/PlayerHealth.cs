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
        [SerializeField] private AudioClip hurtSound;

        public float Max => maxHealth;
        public float Current { get; private set; }
        /// <summary>Permanently dead (e.g. caught by the scrolling death camera).
        /// Unlike a normal knock-out this does not respawn.</summary>
        public bool IsDead { get; private set; }

        /// <summary>When set, running out of health kills permanently instead of
        /// respawning. The scrolling camera sets this on the players it manages: in a
        /// scrolling level the spawn point is long gone behind the kill wall, so a
        /// bench-style respawn would strand the player off-screen. (This assembly
        /// cannot reference CameraMovement, hence the push-in flag.)</summary>
        public bool PermanentDeath { get; set; }

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private AudioSource _audio;
        private Color _baseColor;
        private Vector3 _spawnPoint;
        private float _invulnUntil;
        private float _flashUntil;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _audio = GetComponent<AudioSource>();
            if (_sr != null) _baseColor = _sr.color;
            Current = maxHealth;
        }

        private void Start()
        {
            _spawnPoint = transform.position;
        }

        /// <summary>Kill outright with no respawn — the scrolling camera's mechanic:
        /// fall behind once and you're out. The object stays for camera targeting.</summary>
        public void Kill()
        {
            if (IsDead)
                return;
            IsDead = true;
            Current = 0f;
            Particles.Burst(transform.position, new Color(0.9f, 0.2f, 0.2f), 16, 4.5f, 0.14f, 0.7f);

            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;
            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
            if (_rb != null) _rb.simulated = false;
            if (_sr != null) _sr.enabled = false;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || Time.time < _invulnUntil)
                return;

            Current = Mathf.Max(0f, Current - amount);
            _flashUntil = Time.time + 0.12f;
            if (hurtSound != null && _audio != null)
                _audio.PlayOneShot(hurtSound);
            Particles.Burst(transform.position, new Color(0.9f, 0.2f, 0.2f), 10, 3.5f, 0.12f, 0.5f);

            if (Current <= 0f)
            {
                if (PermanentDeath)
                    Kill();
                else
                    Respawn();
            }
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
            if (_sr == null || IsDead)
                return;
            bool invuln = Time.time < _invulnUntil;
            if (Time.time < _flashUntil)
                _sr.color = Color.red;
            else
                _sr.color = invuln ? _baseColor * new Color(1f, 1f, 1f, 0.5f) : _baseColor;
        }
    }
}
