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
        [Tooltip("1/2/3 switch fists/axe/wrecking ball — sandbox stand-in for weapon pickups.")]
        [SerializeField] private bool weaponHotkeys = true;

        [Header("Damage per material")]
        [SerializeField] private DamageProfile fistDamage = new DamageProfile(35f, 12f, 5f, 25f, 8f);
        [SerializeField] private DamageProfile axeDamage = new DamageProfile(70f, 40f, 20f, 25f, 25f);
        [SerializeField] private DamageProfile wreckingDamage = new DamageProfile(45f, 65f, 40f, 25f, 50f);

        [Header("No-animator fallback")]
        [Tooltip("Swing cadence when there is no Animator to fire applyDamage events.")]
        [SerializeField] private float fallbackSwingInterval = 0.35f;
        [SerializeField] private bool showSwingFlash = false;

        [Header("Dynamite")]
        [SerializeField] private GameObject dynamitePrefab;
        [SerializeField] private int dynamiteCount = 3;
        [SerializeField] private float throwForce = 400f;

        [Header("Audio")]
        [SerializeField] private AudioClip hitSound;

        private static readonly int PunchParam = Animator.StringToHash("IsPunching");
        private static readonly int AxeParam = Animator.StringToHash("IsAxeing");
        private static readonly int WreckParam = Animator.StringToHash("IsWrecking");

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private Animator _anim;
        private AudioSource _audio;
        private bool _canDynamite = true;
        private float _nextFallbackSwing;
        private readonly HashSet<BuildingBlock> _hitThisSwing = new HashSet<BuildingBlock>();

        public WeaponType Weapon { get => weapon; set => weapon = value; }
        public int DynamiteCount { get => dynamiteCount; set => dynamiteCount = Mathf.Max(0, value); }
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
            _anim = GetComponent<Animator>();
            _audio = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (weaponHotkeys && _input.WeaponHotkey != 0)
                weapon = (WeaponType)(_input.WeaponHotkey - 1);

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

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.AddForce(new Vector2(throwForce * facing, throwForce / 3f));
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
