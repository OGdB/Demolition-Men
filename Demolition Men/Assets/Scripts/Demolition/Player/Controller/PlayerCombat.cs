using System.Collections.Generic;
using UnityEngine;

namespace Demolition
{
    public enum WeaponType
    {
        Fists,
        Axe,
        WreckingBall,
    }

    /// <summary>
    /// Attack + dynamite with the original PlayerAttack's UX. Holding attack raises the
    /// matching animator bool (IsPunching / IsAxeing / IsWrecking) so the swing clip
    /// loops, and the clip's <c>applyDamage</c> animation events decide when each swing
    /// lands — identical rhythm to the original (Punching_1 hits twice per loop).
    ///
    /// Damage is per block material per weapon, keeping the original weapon identities:
    /// fists barely dent masonry, the axe is the wood specialist, the wrecking ball is
    /// the brick/metal specialist. Without an Animator (the primitive demo player)
    /// swings land on a fixed cadence instead.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
    public class PlayerCombat : MonoBehaviour
    {
        [System.Serializable]
        public struct DamageProfile
        {
            public float wood;
            public float brick;
            public float metal;
            public float glass;
            public float support;

            public DamageProfile(float wood, float brick, float metal, float glass, float support)
            {
                this.wood = wood;
                this.brick = brick;
                this.metal = metal;
                this.glass = glass;
                this.support = support;
            }

            public float For(BlockMaterial material)
            {
                switch (material)
                {
                    case BlockMaterial.Wood:    return wood;
                    case BlockMaterial.Brick:   return brick;
                    case BlockMaterial.Metal:   return metal;
                    case BlockMaterial.Glass:   return glass;
                    case BlockMaterial.Support: return support;
                    default:                    return 0f;
                }
            }
        }

        [Header("Attack")]
        [Tooltip("Overlap circle centre relative to the feet; x is mirrored by facing.")]
        [SerializeField] private Vector2 attackOffset = new Vector2(1.25f, 1.3f);
        [SerializeField] private float attackRadius = 1f;
        [SerializeField] private float attackForce = 500f;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private WeaponType weapon = WeaponType.Fists;

        [Header("Damage per material")]
        [SerializeField] private DamageProfile fistDamage = new DamageProfile(35f, 12f, 5f, 25f, 8f);
        [SerializeField] private DamageProfile axeDamage = new DamageProfile(70f, 40f, 20f, 25f, 25f);
        [SerializeField] private DamageProfile wreckingDamage = new DamageProfile(45f, 65f, 40f, 25f, 50f);

        [Header("Headbutt")]
        [Tooltip("Jumping head-first into a block damages it: fist damage times this.")]
        [SerializeField] private float headbuttFactor = 2f;
        [Tooltip("Upward speed below which hitting a block from below does nothing.")]
        [SerializeField] private float headbuttMinSpeed = 3f;
        [Tooltip("Upward speed at which a headbutt does its full damage.")]
        [SerializeField] private float headbuttFullSpeed = 12f;
        [Tooltip("Contact this high above the feet counts as the head.")]
        [SerializeField] private float headHeight = 1.2f;

        [Header("No-animator fallback")]
        [Tooltip("Swing cadence when there is no Animator to fire applyDamage events.")]
        [SerializeField] private float fallbackSwingInterval = 0.35f;
        [SerializeField] private bool showSwingFlash = false;

        [Header("Dynamite")]
        [SerializeField] private GameObject dynamitePrefab;
        [SerializeField] private int dynamiteCount = 3;
        [SerializeField] private int maxDynamite = 9;
        [SerializeField] private float throwForce = 400f;

        [Header("Audio")]
        [SerializeField] private AudioClip hitSound;

        private static readonly int PunchParam = Animator.StringToHash("IsPunching");
        private static readonly int AxeParam = Animator.StringToHash("IsAxeing");
        private static readonly int WreckParam = Animator.StringToHash("IsWrecking");

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private Rigidbody2D _rb;
        // Upward speed before this physics step resolved its contacts; 0 once the jump headbutted.
        private float _riseSpeed;
        private Animator _anim;
        private AudioSource _audio;
        private bool _canDynamite = true;
        private float _nextFallbackSwing;
        private readonly HashSet<BuildingBlock> _hitThisSwing = new HashSet<BuildingBlock>();
        // Landed hits left per weapon (indexed by WeaponType); fists never run out.
        private readonly int[] _uses = new int[3];

        public WeaponType Weapon => weapon;
        /// <summary>Landed hits left on the current weapon, -1 for fists.</summary>
        public int WeaponUses => weapon == WeaponType.Fists ? -1 : _uses[(int)weapon];
        public int DynamiteCount { get => dynamiteCount; set => dynamiteCount = Mathf.Max(0, value); }
        public bool DynamiteFull => dynamiteCount >= maxDynamite;
        public float DamageMultiplier { get => damageMultiplier; set => damageMultiplier = Mathf.Max(0f, value); }
        /// <summary>Picked-up weapons get this many times their hits (skill shop).</summary>
        public float WeaponHitMultiplier { get; set; } = 1f;
        public int PlayerIndex => _input.PlayerIndex;

        private Vector2 AttackOrigin =>
            (Vector2)transform.position + new Vector2(attackOffset.x * _motor.Facing, attackOffset.y);

        public void ConfigureForPrimitive(Vector2 offset, bool swingFlash)
        {
            attackOffset = offset;
            showSwingFlash = swingFlash;
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            _rb = GetComponent<Rigidbody2D>();
            _anim = GetComponent<Animator>();
            _audio = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_input.WeaponCycle != 0)
                CycleWeapon(_input.WeaponCycle);

            bool attacking = _input.AttackHeld;
            if (_anim != null)
            {
                _anim.SetBool(PunchParam, attacking && weapon == WeaponType.Fists);
                _anim.SetBool(AxeParam, attacking && weapon == WeaponType.Axe);
                _anim.SetBool(WreckParam, attacking && weapon == WeaponType.WreckingBall);
            }
            else if (attacking && Time.time >= _nextFallbackSwing)
            {
                _nextFallbackSwing = Time.time + fallbackSwingInterval;
                applyDamage();
            }

            if (_input.DynamiteDown && dynamiteCount > 0 && _canDynamite)
                ThrowDynamite();
        }

        private DamageProfile CurrentDamage
        {
            get
            {
                switch (weapon)
                {
                    case WeaponType.Axe:          return axeDamage;
                    case WeaponType.WreckingBall: return wreckingDamage;
                    default:                      return fistDamage;
                }
            }
        }

        // Named for the applyDamage animation events authored on the original
        // Punching/Axing/Wrecking clips — the animation decides when a swing lands.
        private void applyDamage()
        {
            if (!isActiveAndEnabled)
                return; // animation events reach disabled components: a proxy's or a stopped player's swing
            Vector2 origin = AttackOrigin;

            if (showSwingFlash)
                SpawnSwingFlash(origin);

            _hitThisSwing.Clear();
            bool hitAnything = false;
            foreach (var hit in Physics2D.OverlapCircleAll(origin, attackRadius))
            {
                var block = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                if (block == null || !_hitThisSwing.Add(block))
                    continue;

                hitAnything = true;
                block.TakeDamage(CurrentDamage.For(block.Material) * damageMultiplier, _input.PlayerIndex);

                // Only shoves loose debris; a supported (static) wall just takes damage.
                if (block != null && block.IsFalling)
                {
                    var rb = block.GetComponent<Rigidbody2D>();
                    if (rb != null)
                        rb.AddForce(new Vector2(attackForce * _motor.Facing * damageMultiplier, 0f));
                }
            }

            if (hitAnything && hitSound != null && _audio != null)
                _audio.PlayOneShot(hitSound);

            if (hitAnything && weapon != WeaponType.Fists && --_uses[(int)weapon] <= 0)
                weapon = WeaponType.Fists;
        }

        private void FixedUpdate() => _riseSpeed = _rb != null ? _rb.linearVelocity.y : 0f;

        // Jumping into a standing block from below damages it (one block per jump), harder the
        // faster the jump was still rising. Glass is the motor's job: it shatters on any touch.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Collision messages reach disabled components too (countdown, out of play).
            if (!isActiveAndEnabled || _riseSpeed < headbuttMinSpeed)
                return;
            var block = collision.collider.GetComponentInParent<BuildingBlock>();
            if (block == null || block.IsFalling || block.Material == BlockMaterial.Glass)
                return;
            ContactPoint2D contact = collision.GetContact(0);
            if (Mathf.Abs(contact.normal.y) < 0.7f || contact.point.y < transform.position.y + headHeight)
                return; // a wall at the side, or the feet
            float strength = Mathf.Clamp01(_riseSpeed / headbuttFullSpeed);
            _riseSpeed = 0f;
            block.TakeDamage(fistDamage.For(block.Material) * headbuttFactor * strength * damageMultiplier, _input.PlayerIndex);
            if (hitSound != null && _audio != null)
                _audio.PlayOneShot(hitSound);
        }

        /// <summary>Pick up a weapon: add landed hits and switch to it.</summary>
        public void AddWeapon(WeaponType type, int hits)
        {
            if (type == WeaponType.Fists || hits <= 0)
                return;
            _uses[(int)type] += Mathf.Max(1, Mathf.RoundToInt(hits * WeaponHitMultiplier));
            weapon = type;
        }

        /// <summary>Online, a proxy: show what its owner carries (the owner's device decides it).</summary>
        public void ShowKit(WeaponType type, int uses, int dynamite)
        {
            weapon = type;
            if (type != WeaponType.Fists)
                _uses[(int)type] = uses;
            dynamiteCount = Mathf.Max(0, dynamite);
        }

        /// <returns>False when already carrying the maximum.</returns>
        public bool AddDynamite(int amount)
        {
            if (dynamiteCount >= maxDynamite)
                return false;
            dynamiteCount = Mathf.Min(maxDynamite, dynamiteCount + amount);
            return true;
        }

        // Next/previous weapon that still has hits; fists are always available.
        private void CycleWeapon(int direction)
        {
            for (int step = 1; step < _uses.Length; step++)
            {
                int candidate = ((int)weapon + direction * step + _uses.Length) % _uses.Length;
                if (candidate == (int)WeaponType.Fists || _uses[candidate] > 0)
                {
                    weapon = (WeaponType)candidate;
                    return;
                }
            }
        }

        private void ThrowDynamite()
        {
            _canDynamite = false;
            dynamiteCount--;

            int facing = _motor.Facing;
            Vector3 pos = transform.position + new Vector3(1f * facing, 1.3f, 0f);
            GameObject go = dynamitePrefab != null
                ? Instantiate(dynamitePrefab, pos, Quaternion.identity)
                : Dynamite.CreatePrimitive(pos);

            var dynamite = go.GetComponent<Dynamite>();
            if (dynamite == null)
                dynamite = go.AddComponent<Dynamite>();
            dynamite.Init(this, GetComponents<Collider2D>());

            var force = new Vector2(throwForce * facing, throwForce / 3f);
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.AddForce(force);
            NetRole.DynamiteThrown?.Invoke(dynamite, force);
        }

        /// <summary>Online: fly a harmless copy of the other device's stick (<see cref="Dynamite.Cosmetic"/>).</summary>
        public Dynamite ThrowCosmeticDynamite(Vector3 position, Vector2 force)
        {
            GameObject go = dynamitePrefab != null
                ? Instantiate(dynamitePrefab, position, Quaternion.identity)
                : Dynamite.CreatePrimitive(position);
            var dynamite = go.GetComponent<Dynamite>();
            if (dynamite == null)
                dynamite = go.AddComponent<Dynamite>();
            dynamite.Cosmetic = true;
            dynamite.Init(null, GetComponents<Collider2D>());
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.AddForce(force);
            return dynamite;
        }

        /// <summary>Called by the thrown dynamite once it has finished exploding.</summary>
        public void NotifyDynamiteDone() => _canDynamite = true;

        private void SpawnSwingFlash(Vector2 origin)
        {
            var go = new GameObject("SwingFlash");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Disc();
            sr.color = new Color(1f, 0.85f, 0.35f, 0.5f);
            sr.sortingOrder = 61;
            go.AddComponent<SpriteFlash>().Configure(attackRadius * 2f, 0.13f);
        }

        private void OnDrawGizmosSelected()
        {
            int facing = Application.isPlaying && _motor != null ? _motor.Facing : 1;
            Vector2 origin = (Vector2)transform.position + new Vector2(attackOffset.x * facing, attackOffset.y);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, attackRadius);
        }
    }
}
