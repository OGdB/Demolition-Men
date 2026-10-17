using System.Collections.Generic;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// One destructible cell in a <see cref="DestructibleBuilding"/>.
    ///
    /// Option C lifecycle:
    ///  - <b>Supported</b>: Rigidbody2D is Static (near-zero solver cost, no stacking jitter).
    ///    Under structural stress it visually leans/sags/trembles so players can anticipate
    ///    a collapse — sprite only, the collider never moves.
    ///  - <b>Falling</b>: on detach it flips to Dynamic and can hurt the player on impact.
    ///  - <b>Rubble</b>: once it settles it re-freezes to Static and stays around as debris.
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

        [Header("Fall impact (self-damage)")]
        [Tooltip("Relative impact speed below which a falling block lands without crumbling.")]
        [SerializeField] private float selfDamageMinImpact = 4f;
        [Tooltip("Damage the block deals to itself per unit of impact speed above the minimum.")]
        [SerializeField] private float selfDamageScale = 12f;

        [Header("Stress visuals (pre-collapse anticipation)")]
        [SerializeField] private float maxLeanDegrees = 7f;
        [SerializeField] private float maxSag = 0.12f;
        [SerializeField] private float maxDrift = 0.22f;
        [SerializeField] private float visualLerpSpeed = 4f;
        [SerializeField] private float trembleThreshold = 0.55f;
        [SerializeField] private float trembleAmplitude = 0.045f;

        public BlockMaterial Material => material;
        /// <summary>Grid cells this block occupies (one for unit bricks, several for
        /// beams/platforms/walls). Empty while unregistered or for cosmetic riders.</summary>
        public IReadOnlyList<Vector2Int> Coords => _coords;
        public bool IsFalling { get; private set; }
        public bool IsRubble { get; private set; }
        /// <summary>Current cosmetic stress, 0 (solid) .. 1 (about to give).</summary>
        public float Stress { get; private set; }
        /// <summary>Player who last damaged this block, -1 if none.</summary>
        public int LastInstigator { get; private set; } = -1;
        public DestructibleBuilding Building => _building;
        public float HealthFraction => _resolvedMaxHealth > 0f ? Mathf.Clamp01(_health / _resolvedMaxHealth) : 0f;

        private readonly List<Vector2Int> _coords = new List<Vector2Int>();
        private DestructibleBuilding _building;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Transform _visual;
        // Authored pose of the visual (a rotated or scaled sprite); the stress lean is applied on top.
        private Vector3 _visualBasePos;
        private Quaternion _visualBaseRot = Quaternion.identity;
        private Vector3 _smoothVisualPos;
        private Color _baseColor;
        private float _health;
        private float _resolvedMaxHealth;
        private float _settleTimer;
        // Settled once already: keeps the rubble dimming if it is knocked loose again.
        private bool _wasRubble;
        private float _lastImpactTime = -999f;
        private bool _destroyed;
        // Points for this block were already awarded (collapse credit, or a cosmetic
        // rider released by its host), so destroying it later scores nothing.
        private bool _credited;
        private SpriteRenderer _cracks;
        private int _crackStage;
        private int _crackVariant;
        private float _targetLeanDeg;
        private float _targetSag;
        private float _targetDrift;
        private float _tremblePhase;
        private float _trembleBoost = 1f;

        /// <summary>Registered with a building (loose, unregistered blocks still take
        /// damage and die; they just trigger no structural collapse).</summary>
        public bool IsRegistered => _building != null;

        // Part of the standing structure, which online the host decides for both screens.
        private bool IsStanding => _building != null && _coords.Count > 0 && !IsFalling && !IsRubble;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Static;

            // Prefab-authored blocks are functional even before (or without) a
            // DestructibleBuilding adopting them; Initialise re-runs this harmlessly.
            _resolvedMaxHealth = maxHealth > 0f ? maxHealth : BlockMaterialInfo.DefaultHealth(material);
            _health = _resolvedMaxHealth;
            _tremblePhase = Random.value * Mathf.PI * 2f;
            EnsureVisualChild();
        }

        /// <summary>Authoring (generated buildings): set the material before the block is registered.</summary>
        public void SetMaterial(BlockMaterial mat) => material = mat;

        /// <summary>Code-driven registration (bootstrap/tests): one cell, explicit material.</summary>
        public void Initialise(DestructibleBuilding building, Vector2Int coord, BlockMaterial mat)
        {
            material = mat;
            Initialise(building, new List<Vector2Int> { coord });
        }

        /// <summary>Adopt this block into a building on the given cells, keeping the
        /// material it was authored with. An empty/null list marks a cosmetic rider
        /// (a background block that just falls when its host does).</summary>
        public void Initialise(DestructibleBuilding building, List<Vector2Int> coords)
        {
            _building = building;
            _coords.Clear();
            if (coords != null)
                _coords.AddRange(coords);
            _resolvedMaxHealth = maxHealth > 0f ? maxHealth : BlockMaterialInfo.DefaultHealth(material);
            _health = _resolvedMaxHealth;
            EnsureVisualChild();
        }

        /// <summary>
        /// The stress lean must never move the collider, so the sprite lives on a child
        /// transform that the visuals can offset/rotate freely. If the sprite was
        /// authored on the root (as the demo bootstrap does), migrate it to a child.
        /// </summary>
        private void EnsureVisualChild()
        {
            if (_visual != null)
                return; // already migrated (Awake ran before Initialise)

            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                var child = new GameObject("Visual");
                child.transform.SetParent(transform, false);
                var childSr = child.AddComponent<SpriteRenderer>();
                childSr.sprite = rootSr.sprite;
                childSr.color = rootSr.color;
                childSr.sortingLayerID = rootSr.sortingLayerID;
                childSr.sortingOrder = rootSr.sortingOrder;
                Destroy(rootSr);
                _sr = childSr;
            }
            else
            {
                _sr = GetComponentInChildren<SpriteRenderer>();
            }

            _visual = _sr != null ? _sr.transform : null;
            if (_visual != null)
            {
                _visualBasePos = _visual.localPosition;
                _visualBaseRot = _visual.localRotation;
            }
            if (_sr != null)
                _baseColor = _sr.color;
        }

        /// <param name="instigator">Player index dealing the damage, -1 for none.</param>
        public void TakeDamage(float amount, int instigator = -1)
        {
            if (_destroyed || amount <= 0f)
                return;

            // Chips of debris + progressive darkening as the block weakens.
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(material), 5, 3f, 0.1f, 0.4f);
            if (NetRole.IsClient && IsStanding)
            {
                // The host applies it and answers with the new health (ApplyNetHealth).
                NetRole.RequestDamage?.Invoke(this, amount);
                return;
            }

            if (instigator >= 0)
                LastInstigator = instigator;
            _health -= amount;
            RefreshColor();
            if (NetRole.IsHost && IsStanding)
                NetRole.BlockHealthChanged?.Invoke(this);

            if (_health <= 0f)
                Destroyed();
        }

        /// <summary>Client: the host's health for this block (fraction of its maximum). At 0
        /// it is destroyed through the normal path, so the same cells detach as on the host.</summary>
        public void ApplyNetHealth(float fraction, int instigator)
        {
            if (_destroyed)
                return;
            LastInstigator = instigator;
            _health = Mathf.Min(_health, fraction * _resolvedMaxHealth); // never heals: the tint stays monotonic
            RefreshColor();
            if (fraction <= 0f)
                Destroyed();
        }

        /// <summary>Destroy the block outright — glass panes on impact.</summary>
        public void Shatter(int instigator = -1) => TakeDamage(Mathf.Max(_health, 1f), instigator);

        /// <summary>The building scored this block's collapse; its later destruction pays nothing.</summary>
        internal void MarkCredited() => _credited = true;

        /// <summary>
        /// Single source of truth for the sprite tint: damage darkening × rubble dimming,
        /// applied to RGB only. Alpha is never touched, so a block can't read as
        /// "transparent/ghostly", and because health only ever decreases the tint is
        /// monotonic — a hit can never make a block look healthier.
        /// </summary>
        private void RefreshColor()
        {
            if (_sr == null)
                return;
            float damageFrac = _resolvedMaxHealth > 0f ? Mathf.Clamp01(_health / _resolvedMaxHealth) : 1f;
            float factor = (0.45f + 0.55f * damageFrac) * (_wasRubble ? 0.7f : 1f);
            _sr.color = new Color(
                _baseColor.r * factor, _baseColor.g * factor, _baseColor.b * factor, _baseColor.a);
            RefreshCracks(damageFrac);
        }

        private static readonly Dictionary<Sprite, bool> _opaqueSprites = new Dictionary<Sprite, bool>();
        private static readonly List<Vector2> _shapePoints = new List<Vector2>();

        // Opaque = its physics outline (traced from the pixels on import) fills its rectangle.
        private static bool IsOpaque(Sprite sprite)
        {
            if (sprite == null)
                return true;
            if (_opaqueSprites.TryGetValue(sprite, out bool opaque))
                return opaque;
            float area = 0f;
            int shapes = sprite.GetPhysicsShapeCount();
            for (int i = 0; i < shapes; i++)
            {
                sprite.GetPhysicsShape(i, _shapePoints);
                float twice = 0f; // shoelace formula
                for (int p = 0; p < _shapePoints.Count; p++)
                {
                    Vector2 a = _shapePoints[p], b = _shapePoints[(p + 1) % _shapePoints.Count];
                    twice += a.x * b.y - b.x * a.y;
                }
                area += Mathf.Abs(twice) * 0.5f;
            }
            Vector2 size = sprite.bounds.size;
            opaque = shapes == 0 || area >= size.x * size.y * 0.95f;
            _opaqueSprites[sprite] = opaque;
            return opaque;
        }

        // Cracks on top of the darkening: stage 1..3 as health drops, never healing. A child
        // of the visual, so it leans with the sprite.
        private void RefreshCracks(float healthFraction)
        {
            int stage = healthFraction > 0.75f ? 0 : healthFraction > 0.45f ? 1 : healthFraction > 0.2f ? 2 : 3;
            if (stage <= _crackStage || _visual == null || material == BlockMaterial.Glass)
                return;
            _crackStage = stage;
            if (_cracks == null)
            {
                // Same sorting order as the visual, a hair closer to the camera. Sprites with
                // see-through parts (planks, trusses, thin columns) clip the cracks with a mask
                // on a child (one Renderer per GameObject); its range covers only that order.
                int order = _sr.sortingOrder;
                bool masked = !IsOpaque(_sr.sprite);
                if (masked)
                {
                    var maskGo = new GameObject("CrackMask");
                    maskGo.transform.SetParent(_visual, false);
                    var mask = maskGo.AddComponent<SpriteMask>();
                    mask.sprite = _sr.sprite;
                    mask.isCustomRangeActive = true;
                    mask.frontSortingLayerID = mask.backSortingLayerID = _sr.sortingLayerID;
                    mask.frontSortingOrder = order;
                    mask.backSortingOrder = order - 1;
                }

                var go = new GameObject("Cracks");
                go.transform.SetParent(_visual, false);
                _cracks = go.AddComponent<SpriteRenderer>();
                _cracks.sortingLayerID = _sr.sortingLayerID;
                _cracks.sortingOrder = order;
                _cracks.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
                Bounds bounds = _sr.sprite != null ? _sr.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
                go.transform.localPosition = bounds.center + new Vector3(0f, 0f, -0.01f);
                go.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
                Vector3 p = transform.position;
                _crackVariant = Mathf.FloorToInt(p.x * 7f + p.y * 13f); // picked once: a falling block keeps its pattern
            }
            _cracks.sprite = PrimitiveSprite.Cracks(stage, _crackVariant);
        }

        private void Destroyed()
        {
            _destroyed = true;
            Particles.Burst(transform.position, BlockMaterialInfo.Tint(material), 16, 5f, 0.16f, 0.75f);
            if (!_credited)
            {
                _credited = true;
                DestructionEvents.RaiseBlockDestroyed(new BlockDestroyedInfo(
                    material, Mathf.Max(1, _coords.Count), LastInstigator, transform.position));
            }
            if (_building != null)
                _building.OnBlockDestroyed(this);
            Vanish();
        }

        /// <summary>Remove the block without the destruction effects (debris over the
        /// falling cap, gates crumbling to dust), first waking any rubble it held up.</summary>
        public void Vanish()
        {
            WakeRubbleAround();
            Destroy(gameObject);
        }

        // Shared scratch (main thread only): big collapses wake a lot without allocating.
        private static readonly Collider2D[] _touching = new Collider2D[32];
        private static readonly Queue<BuildingBlock> _toWake = new Queue<BuildingBlock>();

        // Settled rubble is static and outside the support graph, so nothing else tells it
        // that what it rests on (or is wedged against) has gone: wake the rubble this block
        // may be holding up, then whatever those held up, breadth-first. Rubble lying beside
        // or below on the same footing stays put, so breaking one block doesn't stir a pile.
        private void WakeRubbleAround()
        {
            EnqueueTouchingRubble(this);
            while (_toWake.Count > 0)
            {
                BuildingBlock rubble = _toWake.Dequeue();
                if (rubble == null || !rubble.IsRubble)
                    continue;
                rubble.Wake();
                EnqueueTouchingRubble(rubble);
            }
        }

        private static void EnqueueTouchingRubble(BuildingBlock block)
        {
            var collider = block.GetComponent<Collider2D>();
            if (collider == null)
                return;
            Bounds b = collider.bounds;
            int count = Physics2D.OverlapBox(b.center, (Vector2)b.size + new Vector2(0.2f, 0.2f), 0f, ContactFilter2D.noFilter, _touching);
            for (int i = 0; i < count; i++)
            {
                var other = _touching[i] != null ? _touching[i].GetComponentInParent<BuildingBlock>() : null;
                if (other != null && other != block && other.IsRubble && _touching[i].bounds.min.y > b.min.y + 0.1f)
                    _toWake.Enqueue(other);
            }
        }

        // Rubble whose footing went: falls again until it settles somewhere else.
        private void Wake()
        {
            IsRubble = false;
            IsFalling = true;
            _settleTimer = 0f;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 1f;
        }

        /// <summary>
        /// Called by the building after each destruction event: how precarious this cell
        /// now is (0..1), which way it should lean (-1 = clockwise, +1 = counter-clockwise,
        /// 0 = level), its sideways bend displacement for loaded columns (normalized;
        /// negative = left, 0 for horizontally-carried cells), and how far past its
        /// baseline load capacity it is (0..∞) — a crumbling support rattles harder the
        /// further past capacity it is pushed.
        /// </summary>
        public void SetStress(float stress, float lean, float drift, float overload = 0f)
        {
            Stress = Mathf.Clamp01(stress);
            _targetLeanDeg = lean * Stress * maxLeanDegrees;
            _targetSag = Stress * Stress * maxSag;
            _targetDrift = drift * Stress * maxDrift;
            _trembleBoost = 1f + Mathf.Min(2f, overload);
        }

        private void Update()
        {
            if (_visual == null)
                return;

            float targetLean = _targetLeanDeg;
            float targetSag = _targetSag;
            float targetDrift = _targetDrift;
            float stress = Stress;
            if (IsFalling || IsRubble)
            {
                // Real physics has taken over — ease the fake lean back out.
                targetLean = 0f;
                targetSag = 0f;
                targetDrift = 0f;
                stress = 0f;
            }

            float k = Time.deltaTime * visualLerpSpeed;
            _smoothVisualPos = Vector3.Lerp(_smoothVisualPos, new Vector3(targetDrift, -targetSag, 0f), k);

            // Tremble is added after the smoothing so the lerp doesn't filter it away.
            float trembleX = 0f;
            if (stress > trembleThreshold)
            {
                float t = (stress - trembleThreshold) / (1f - trembleThreshold);
                trembleX = Mathf.Sin(Time.time * 33f + _tremblePhase) * trembleAmplitude * t * _trembleBoost;
            }

            _visual.localPosition = _visualBasePos + _smoothVisualPos + new Vector3(trembleX, 0f, 0f);
            _visual.localRotation = Quaternion.Lerp(
                _visual.localRotation, Quaternion.Euler(0f, 0f, targetLean) * _visualBaseRot, k);
        }

        /// <summary>Static structure -> falling debris. Called for every detached cell.</summary>
        public void BeginFalling()
        {
            if (IsFalling || IsRubble)
                return;
            IsFalling = true;
            _settleTimer = 0f;

            // Blocks migrated from the old joint-based system may still carry authored
            // joints; a FixedJoint2D with no connected body pins a dynamic body to the
            // world, so strip them the moment real physics takes over.
            foreach (var joint in GetComponents<Joint2D>())
                Destroy(joint);

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 1f;
            _rb.AddForce(new Vector2((Random.value - 0.5f) * detachImpulse, 0f), ForceMode2D.Impulse);
            _rb.AddTorque((Random.value - 0.5f) * detachImpulse);
            WakeRubbleAround(); // debris that had settled on top comes down with it
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
            _wasRubble = true;
            _rb.bodyType = RigidbodyType2D.Static; // leaves the solver; stays visible as debris
            RefreshColor();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsFalling)
                return;

            float impact = collision.relativeVelocity.magnitude;

            // Hurt the player it lands on.
            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health != null && impact >= minImpactSpeed
                && Time.time - _lastImpactTime >= impactCooldown)
            {
                _lastImpactTime = Time.time;
                float damage = Mathf.Clamp((impact - minImpactSpeed) * impactDamageScale, 0f, maxImpactDamage);
                health.TakeDamage(damage);
                Particles.Burst(collision.GetContact(0).point, new Color(0.95f, 0.8f, 0.2f), 8, 4f, 0.12f, 0.5f);
            }

            // A falling pane never survives contact — glass shatters instead of
            // settling into rubble.
            if (material == BlockMaterial.Glass)
            {
                Shatter();
                return;
            }

            // Falling has real consequences: a hard landing chips the block itself, so the
            // damage tint after a fall reflects health that was genuinely lost — and a big
            // enough drop shatters it outright (TakeDamage handles particles/destruction).
            if (impact >= selfDamageMinImpact)
                TakeDamage((impact - selfDamageMinImpact) * selfDamageScale);
        }
    }
}
