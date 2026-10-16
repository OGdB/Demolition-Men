using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Player health. Falling blocks deal impact damage (see <see cref="BuildingBlock"/>).
    /// In a run, running out of health raises <see cref="Depleted"/> and the run
    /// controller decides what happens. On the test bench the player respawns at its
    /// start position instead.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float respawnInvulnerability = 1.5f;
        [SerializeField] private AudioClip hurtSound;

        public float Max => maxHealth;
        public float Current { get; private set; }
        /// <summary>Out of play: hidden, no physics, no controls (see <see cref="Kill"/>).</summary>
        public bool IsDead { get; private set; }

        /// <summary>Set by the run controller: depletion only raises <see cref="Depleted"/>
        /// instead of the bench respawn.</summary>
        public bool ManagedByRun { get; set; }

        /// <summary>Health reached 0 while <see cref="ManagedByRun"/>.</summary>
        public event System.Action<PlayerHealth> Depleted;

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

        /// <summary>Take the player out of play. The object stays so it can be revived.</summary>
        public void Kill()
        {
            if (IsDead)
                return;
            IsDead = true;
            Current = 0f;
            Particles.Burst(transform.position, new Color(0.9f, 0.2f, 0.2f), 16, 4.5f, 0.14f, 0.7f);
            SetInPlay(false);
        }

        /// <summary>Bring a killed player back at <paramref name="position"/> with full
        /// health and a short invulnerability.</summary>
        public void Revive(Vector3 position)
        {
            if (!IsDead)
                return;
            IsDead = false;
            transform.position = position;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
            Current = maxHealth;
            _invulnUntil = Time.time + respawnInvulnerability;
            SetInPlay(true);
            Particles.Burst(transform.position, new Color(1f, 0.85f, 0.3f), 16, 4.5f, 0.14f, 0.7f);
        }

        private void SetInPlay(bool inPlay)
        {
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = inPlay;
            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = inPlay;
            if (_rb != null) _rb.simulated = inPlay;
            if (_sr != null) _sr.enabled = inPlay;
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
                if (ManagedByRun)
                    Depleted?.Invoke(this);
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
