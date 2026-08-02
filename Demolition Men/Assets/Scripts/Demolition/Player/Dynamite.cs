using System.Collections.Generic;
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
        [SerializeField] private bool explodeOnContact = true;
        [Tooltip("Optional explosion prefab, spawned by the Effect animation event.")]
        [SerializeField] private GameObject effect;

        private PlayerCombat _owner;
        private Rigidbody2D _rb;
        private Animator _anim;
        private AudioSource _audio;
        private float _fuse;
        private bool _exploded;

        private static readonly int BoomParam = Animator.StringToHash("boom");

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
            if (_fuse <= 0f)
                Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (explodeOnContact && !_exploded && other.GetComponentInParent<BuildingBlock>() != null)
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

            var damaged = new HashSet<BuildingBlock>();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, areaOfEffect * 0.5f))
            {
                var block = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                if (block != null && damaged.Add(block))
                    block.TakeDamage(damage);
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

            if (_anim != null)
            {
                // Explosion.anim takes over: it fires Effect, then destroyDynamite.
                _anim.SetBool(BoomParam, true);
                Invoke(nameof(destroyDynamite), 5f); // failsafe if the clip's event is missing
            }
            else
            {
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
                Instantiate(effect, transform.position, Quaternion.identity);
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
