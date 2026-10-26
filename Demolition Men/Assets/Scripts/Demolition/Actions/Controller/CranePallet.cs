using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// The crane's answer to the whistle: a target marker where the player pointed, then a
    /// heavy pallet of bricks dropping from above the screen onto it. A real body: it lands on
    /// whatever is there and smashes through, up to three impacts, each wrecking the blocks
    /// around it, throwing debris and players aside, before it breaks apart.
    /// </summary>
    public sealed class CranePallet : MonoBehaviour
    {
        private const float Delay = 0.9f; // the crane swinging over
        private const int MaxImpacts = 3;
        private static readonly float[] Radius = { 2.3f, 1.8f, 1.4f };
        private static readonly float[] Damage = { ActionStrikes.Wreck, 400f, 250f };

        private int _who;
        private bool _authoritative;
        private int _impacts;
        private float _born, _restingSince = -1f;
        private Rigidbody2D _rb;

        public static void Call(Vector2 pointedAt, Vector2 target, int who, bool authoritative)
        {
            var host = new GameObject("CraneCall");
            host.AddComponent<CraneCall>().Run(pointedAt, target, who, authoritative, Delay);
        }

        /// <summary>Drop a pallet from <paramref name="top"/>.</summary>
        public static void Drop(Vector2 top, int who, bool authoritative)
        {
            var go = new GameObject("Pallet");
            go.transform.position = top;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ActionSprites.Prop("pallet");
            sr.sortingOrder = 15;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 30f;
            rb.gravityScale = 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            var box = go.AddComponent<BoxCollider2D>();
            if (sr.sprite != null)
            {
                box.size = sr.sprite.bounds.size;
                box.offset = sr.sprite.bounds.center;
            }
            CranePallet pallet = go.AddComponent<CranePallet>();
            pallet._who = who;
            pallet._authoritative = authoritative;
            pallet._rb = rb;
            pallet._born = Time.time;
            ActionAudio.Falling();
        }

        private void Update()
        {
            if (Time.time - _born > 8f || transform.position.y < -40f)
                Break();
            // Landed and still: nothing left under it to break.
            bool still = _impacts > 0 && _rb.linearVelocity.sqrMagnitude < 0.25f;
            if (!still)
                _restingSince = -1f;
            else if (_restingSince < 0f)
                _restingSince = Time.time;
            else if (Time.time - _restingSince > 0.4f)
                Break();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(20f); // ouch: the crane can't see you
                return;
            }
            if (collision.relativeVelocity.magnitude < 4f)
            {
                if (_impacts > 0)
                    Break(); // came to rest on something it can't break
                return;
            }
            Impact(collision.GetContact(0).point);
        }

        private void Impact(Vector2 at)
        {
            int n = _impacts++;
            ActionStrikes.Hit(ActionStrikes.BlocksInCircle(at, Radius[n]), Damage[n], _who, _authoritative);
            ActionStrikes.Shove(at, Radius[n] + 1.5f, 11f, new Vector2(0f, 4f));
            ActionStrikes.KnockPlayers(at, Radius[n] + 0.5f, 10f, null);
            Particles.Burst(at, new Color(0.75f, 0.7f, 0.6f, 0.9f), 30, 6f, 0.28f, 0.9f);
            Particles.Burst(at, BlockMaterialInfo.Tint(BlockMaterial.Brick), 14, 5f, 0.16f, 0.7f);
            ActionStrikes.Ring(at, Radius[n] * 2.4f, new Color(1f, 0.9f, 0.7f, 0.7f), 0.3f);
            Run.CameraShake.Add(0.6f - n * 0.15f);
            ActionAudio.Slam();
            if (_impacts >= MaxImpacts)
                Break();
            else
                _rb.linearVelocity = new Vector2(0f, Mathf.Min(_rb.linearVelocity.y, -6f)); // keeps going through what broke
        }

        private void Break()
        {
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(BlockMaterial.Brick), 24, 4f, 0.2f, 0.8f);
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(BlockMaterial.Wood), 10, 3f, 0.16f, 0.6f);
            Destroy(gameObject);
        }
    }
}
