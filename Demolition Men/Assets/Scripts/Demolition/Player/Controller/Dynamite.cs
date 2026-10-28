using System.Collections;
using System.Collections.Generic;
using Demolition.Ranked;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Port of the original dynamiteScript for the destruction system: thrown with B,
    /// explodes on a fuse timer or on touching a building block. Reuses the original
    /// Dynamite prefab's animator — <see cref="Effect"/> and <see cref="destroyDynamite"/>
    /// are the animation events authored on Explosion.anim. Damage lands in half the
    /// area of effect (original semantics); loose debris in the full area gets an
    /// outward shove.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Dynamite : MonoBehaviour
    {
        [SerializeField] private float damage = 300f;
        [SerializeField] private float pushForce = 20f;
        [SerializeField] private float fuseTime = 2f;
        [SerializeField] private float areaOfEffect = 3f;
        [Tooltip("Damage to a player at the centre of the blast, falling to 30% at the edge of the area of effect.")]
        [SerializeField] private float playerDamage = 45f;
        [Tooltip("Speed a player is thrown away from the centre of the blast, falling to 40% at the edge.")]
        [SerializeField] private float playerKnockback = 14f;
        [SerializeField] private bool explodeOnContact = true;
        [Tooltip("Optional explosion prefab, spawned by the Effect animation event.")]
        [SerializeField] private GameObject effect;
        [Tooltip("Seconds before the spawned effect is removed (its particle system loops).")]
        [SerializeField] private float effectLifetime = 2f;


        private PlayerCombat _owner;
        private int _instigator = -1; // credited when no thrower owns the stick (a gas tank's blast)
        private Rigidbody2D _rb;
        private Animator _anim;
        private AudioSource _audio;
        private float _fuse;
        private bool _exploded;

        private static readonly int BoomParam = Animator.StringToHash("boom");

        /// <summary>A stick went off here (screen shake and other feedback).</summary>
        public static event System.Action<Vector3> Exploded;

        /// <summary>True while a blast deals its damage: what it breaks was blown up, not hit.</summary>
        public static bool Blasting { get; private set; }

        /// <summary>Online, a copy flown for the other device's player: it looks and sounds the
        /// same but damages nothing (the thrower's device sends the damage), and goes off when
        /// the thrower's stick does (<see cref="ExplodeAt"/>), not on its own fuse.</summary>
        public bool Cosmetic { get; set; }

        /// <summary>What set it off: a thrown stick, a gas tank, an implosion's charge.</summary>
        public HitCause Cause { get; private set; } = HitCause.Dynamite;

        public void ExplodeAt(Vector3 position)
        {
            transform.position = position;
            Explode();
        }

        /// <summary>A gas tank going up (or another <paramref name="cause"/>), credited to <paramref name="instigator"/>,
        /// after <paramref name="delay"/> seconds (a chain of tanks ripples instead of going off in one frame).
        /// It reaches <see cref="RankedRules.GasReach"/> further than a stick: across the free column
        /// between two props, so the next one's tanks go too. Online, a client's blast is only the show:
        /// the host's reaches it as cells.</summary>
        public static void Detonate(GameObject prefab, Vector3 position, int instigator, float delay = 0f, HitCause cause = HitCause.GasTank)
        {
            GameObject go = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : CreatePrimitive(position);
            var dynamite = go.GetComponent<Dynamite>();
            if (dynamite == null)
                dynamite = go.AddComponent<Dynamite>();
            dynamite._instigator = instigator;
            dynamite.Cause = cause;
            dynamite.Cosmetic = NetRole.IsClient;
            dynamite.areaOfEffect *= RankedRules.GasReach;
            if (delay <= 0f)
            {
                dynamite.ExplodeAt(position);
                return;
            }
            dynamite._rb.bodyType = RigidbodyType2D.Static;
            dynamite.SetVisible(false); // nothing to see before the bang
            dynamite.StartCoroutine(dynamite.ExplodeAfter(position, delay));
        }

        private IEnumerator ExplodeAfter(Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            SetVisible(true);
            ExplodeAt(position);
        }

        private void SetVisible(bool visible)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = visible;
        }

        public void Init(PlayerCombat owner, Collider2D[] ignoreColliders)
        {
            _owner = owner;
            if (ignoreColliders == null)
                return;
            foreach (var own in GetComponents<Collider2D>())
                foreach (var other in ignoreColliders)
                    if (other != null)
                        Physics2D.IgnoreCollision(own, other);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _anim = GetComponent<Animator>();
            _audio = GetComponent<AudioSource>();
            _fuse = fuseTime;
        }

        private void FixedUpdate()
        {
            if (_exploded)
                return;
            _fuse -= Time.fixedDeltaTime;
            if (Cosmetic)
            {
                if (_fuse < -2f * fuseTime)
                    Destroy(gameObject); // its thrower's Exploded never came (they left)
                return;
            }
            if (_fuse <= 0f)
                Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (explodeOnContact && !_exploded && !Cosmetic && other.GetComponentInParent<BuildingBlock>() != null)
                Explode();
        }

        private void Explode()
        {
            if (_exploded)
                return;
            _exploded = true;

            _rb.bodyType = RigidbodyType2D.Static;
            transform.rotation = Quaternion.identity;

            if (_audio != null)
                _audio.Play();
            Exploded?.Invoke(transform.position);
            int who = _owner != null ? _owner.PlayerIndex : _instigator;
            if (!Cosmetic)
            {
                RunRecorder.Blew(who, Cause, transform.position); // before the damage it deals
                NetRole.DynamiteExploded?.Invoke(this);
            }

            // Players in the area get hurt, more the closer they stand. Each device hurts only its own
            // player (a proxy ignores damage), so an online copy hurts the player next to it too.
            var hurt = new HashSet<PlayerHealth>();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, areaOfEffect))
            {
                var health = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
                if (health == null || !hurt.Add(health))
                    continue;
                float distance = Vector2.Distance(hit.ClosestPoint(transform.position), transform.position);
                health.TakeDamage(playerDamage * Mathf.Lerp(1f, 0.3f, distance / areaOfEffect), capped: false);
                var motor = health.GetComponent<PlayerMotor>();
                if (motor != null && !health.IsProxy && !health.IsDead)
                {
                    // Away from the blast, always with some lift so the player is thrown, not slid.
                    Vector2 away = (Vector2)health.transform.position + Vector2.up - (Vector2)transform.position;
                    away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
                    away.y = Mathf.Max(away.y, 0.35f);
                    motor.Knockback(away.normalized * (playerKnockback * Mathf.Lerp(1f, 0.4f, distance / areaOfEffect)));
                }
            }

            var damaged = new HashSet<BuildingBlock>();
            if (!Cosmetic)
            {
                Blasting = true;
                foreach (var hit in Physics2D.OverlapCircleAll(transform.position, areaOfEffect * 0.5f))
                {
                    var block = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                    if (block != null && damaged.Add(block))
                        block.TakeDamage(damage, who, Cause, transform.position);
                }
                Blasting = false;
            }

            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, areaOfEffect))
            {
                var block = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                if (block == null || !block.IsFalling)
                    continue;
                var rb = block.GetComponent<Rigidbody2D>();
                if (rb == null)
                    continue;
                Vector2 dir = ((Vector2)block.transform.position - (Vector2)transform.position).normalized;
                rb.AddForce(dir * pushForce, ForceMode2D.Impulse);
            }

            if (_anim != null && _anim.runtimeAnimatorController != null)
            {
                // Explosion.anim takes over: it fires Effect, then destroyDynamite.
                _anim.SetBool(BoomParam, true);
                Invoke(nameof(destroyDynamite), 5f); // failsafe if the clip's event is missing
            }
            else
            {
                Effect();
                Particles.Burst(transform.position, new Color(1f, 0.6f, 0.15f), 24, 7f, 0.2f, 0.6f);
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                    sr.enabled = false;
                Invoke(nameof(destroyDynamite), 0.6f);
            }
        }

        // Animation event (Explosion.anim): first frame of the blast.
        private void Effect()
        {
            if (effect != null)
                Destroy(Instantiate(effect, transform.position, Quaternion.identity), effectLifetime);
        }

        // Animation event (Explosion.anim): last frame — return the "one at a time" token.
        private void destroyDynamite()
        {
            if (_owner != null)
                _owner.NotifyDynamiteDone();
            _owner = null;
            Destroy(gameObject);
        }

        /// <summary>No-assets fallback stick for the code-built demo scene.</summary>
        public static GameObject CreatePrimitive(Vector3 position)
        {
            var go = new GameObject("Dynamite");
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.22f, 0.45f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Unit();
            sr.color = new Color(0.85f, 0.2f, 0.15f);
            sr.sortingOrder = 20;

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = Vector2.one * 1.6f;

            go.AddComponent<Dynamite>();
            return go;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, areaOfEffect * 0.5f);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, areaOfEffect);
        }
    }
}
