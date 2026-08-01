using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// One destructible cell in a <see cref="DestructibleBuilding"/>.
    ///
    /// Option C: while supported, the block's Rigidbody2D is <b>Static</b> (near-zero
    /// solver cost, no stacking instability). It only becomes <b>Dynamic</b> when the
    /// building tells it to fall (<see cref="BeginFalling"/>), i.e. once it has
    /// detached from the foundation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BuildingBlock : MonoBehaviour
    {
        [SerializeField] private BlockMaterial material = BlockMaterial.Brick;
        [SerializeField] private float maxHealth = -1f; // <0 => use material default

        public BlockMaterial Material => material;
        public Vector2Int Coord { get; private set; }
        public bool IsFalling { get; private set; }

        private DestructibleBuilding _building;
        private Rigidbody2D _rb;
        private float _health;

        [Header("Collapse")]
        [Tooltip("Seconds a fallen block lives before it despawns (0 = never).")]
        [SerializeField] private float debrisLifetime = 6f;
        [SerializeField] private float detachImpulse = 0.4f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Static;
        }

        /// <summary>Called by <see cref="DestructibleBuilding"/> at spawn time.</summary>
        public void Initialise(DestructibleBuilding building, Vector2Int coord, BlockMaterial mat)
        {
            _building = building;
            Coord = coord;
            material = mat;
            _health = maxHealth > 0f ? maxHealth : BlockMaterialInfo.DefaultHealth(mat);
        }

        public void TakeDamage(float amount)
        {
            if (IsFalling)
                return; // debris is cosmetic; ignore further structural damage

            _health -= amount;
            if (_health <= 0f)
                Destroyed();
        }

        private void Destroyed()
        {
            if (_building != null)
                _building.OnBlockDestroyed(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Transition from static structure to falling debris. Called by the building
        /// for every cell that detached from the foundation.
        /// </summary>
        public void BeginFalling()
        {
            if (IsFalling)
                return;
            IsFalling = true;

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 1f;
            // A tiny nudge so a detached slab topples instead of dropping in a rigid grid.
            _rb.AddForce(new Vector2((Random.value - 0.5f) * detachImpulse, 0f), ForceMode2D.Impulse);
            _rb.AddTorque((Random.value - 0.5f) * detachImpulse);

            if (debrisLifetime > 0f)
                Destroy(gameObject, debrisLifetime);
        }
    }
}
