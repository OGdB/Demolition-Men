using System.Collections.Generic;
using Demolition.Core;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Owns one building's <see cref="BuildingStructure"/> and the live
    /// <see cref="BuildingBlock"/> instances. When a block is destroyed it re-runs the
    /// support flood-fill, tells newly-detached blocks to fall, and refreshes the
    /// cosmetic per-cell stress (lean/sag) so players can see a collapse coming.
    ///
    /// Two ways to populate:
    ///  - <b>Code-driven</b> (DemoBootstrap / tests): spawn blocks and call
    ///    <see cref="RegisterBlock(BuildingBlock, Vector2Int, bool)"/> per cell.
    ///  - <b>Hand-authored</b> (the game's building prefabs): leave
    ///    <see cref="autoScanChildren"/> on. On Start the building adopts every
    ///    child <see cref="BuildingBlock"/>, infers a cell grid from the collider
    ///    layout (multi-cell blocks like beams and platforms span several cells),
    ///    probes below ground-floor blocks for terrain to find the anchors, and
    ///    registers everything automatically. Background dressing that overlaps an
    ///    already-claimed cell becomes a cosmetic "rider" that simply falls when the
    ///    block in front of it does.
    ///
    /// This is where the discrete, deterministic "which cells exist / which detached"
    /// state lives — the exact thing that would be replicated over NGO (see the
    /// design doc, §Networking). Physics is only ever touched for falling debris.
    /// </summary>
    public class DestructibleBuilding : MonoBehaviour
    {
        [Tooltip("Safety cap on how many blocks may convert to dynamic bodies in a single "
               + "collapse event. Extra detached blocks are removed without simulation.")]
        [SerializeField] private int maxSimultaneousFalling = 200;

        [Tooltip("How far (in cells) the stress heuristic scans sideways for a load-bearing "
               + "column when deciding how much a cell should lean/sag.")]
        [SerializeField] private int stressScanRange = 6;

        [Tooltip("Carried-weight shares at which a column's stress (tremble) saturates.")]
        [SerializeField] private float columnLoadCapacity = 4f;

        [Tooltip("Net lever-arm sum at which a column's lean toward its load saturates.")]
        [SerializeField] private float columnMomentCapacity = 8f;

        [Header("Overload collapse")]
        [Tooltip("How much overload (load/capacity) beyond the intact building's baseline a "
               + "support tolerates before it starts to crumble. Below this it only trembles.")]
        [SerializeField] private float overloadTolerance = 0.6f;

        [Tooltip("Health a crumbling block loses per second, per unit of overload past the "
               + "tolerance. Weak materials give way first; steel supports hold out longest.")]
        [SerializeField] private float crumbleDamageRate = 150f;

        [Tooltip("Seconds between crumble damage ticks (each tick also puffs dust, so this "
               + "paces the visual crumble).")]
        [SerializeField] private float crumbleTickInterval = 0.25f;

        [Header("Hand-authored buildings (auto-scan)")]
        [Tooltip("On Start, adopt every BuildingBlock in children that no code registered "
               + "explicitly: infer the cell grid from collider layout and register it.")]
        [SerializeField] private bool autoScanChildren = true;

        [Tooltip("World size of one structural cell. The hand-built prefabs use 1-unit "
               + "bricks; beams/platforms span a whole number of cells.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("How far below a block to look for non-building static colliders "
               + "(terrain). Blocks resting on ground become the anchors.")]
        [SerializeField] private float anchorProbeDepth = 0.35f;

        // A block footprint larger than this is almost certainly a mis-scaled collider;
        // clamp so one bad asset can't allocate a million cells.
        private const int MaxFootprintCells = 32;

        private readonly BuildingStructure _structure = new BuildingStructure();
        private readonly Dictionary<Vector2Int, BuildingBlock> _blocks =
            new Dictionary<Vector2Int, BuildingBlock>();

        // Cosmetic background blocks that overlap an already-claimed cell: not
        // structural, they just fall when their host block goes.
        private readonly Dictionary<BuildingBlock, List<BuildingBlock>> _riders =
            new Dictionary<BuildingBlock, List<BuildingBlock>>();

        // Stress/overload present in the intact building (e.g. a roof over a hollow
        // interior). Only damage-induced strain beyond this baseline is shown to the
        // player or allowed to crumble a support, so pristine buildings are stable.
        private Dictionary<Vector2Int, CellStress> _baseline;

        // Blocks currently overloaded past their baseline by more than the tolerance,
        // with that beyond-baseline overload. Rebuilt on every destruction event;
        // drained by the crumble tick in Update.
        private readonly Dictionary<BuildingBlock, float> _crumbling =
            new Dictionary<BuildingBlock, float>();
        private readonly List<KeyValuePair<BuildingBlock, float>> _crumbleScratch =
            new List<KeyValuePair<BuildingBlock, float>>();
        private float _crumbleTimer;

        public int StandingBlockCount => _structure.CellCount;
        public int InitialCellCount { get; private set; }

        /// <summary>Fraction of the building destroyed or collapsed, 0..1.</summary>
        public float DestroyedFraction =>
            InitialCellCount == 0 ? 0f : 1f - (float)StandingBlockCount / InitialCellCount;

        /// <summary>Register a code-spawned block occupying a single cell.</summary>
        public void RegisterBlock(BuildingBlock block, Vector2Int coord, bool isAnchor)
        {
            _scratchCoords.Clear();
            _scratchCoords.Add(coord);
            RegisterBlock(block, _scratchCoords, isAnchor);
        }

        private readonly List<Vector2Int> _scratchCoords = new List<Vector2Int>();

        /// <summary>Register a block occupying several cells (a rigid beam/platform/wall
        /// piece). Glass registers as fragile: it hangs in its frame but nothing can
        /// rest on it. Anchor blocks anchor every cell they occupy.</summary>
        public void RegisterBlock(BuildingBlock block, List<Vector2Int> coords, bool isAnchor)
        {
            for (int i = 0; i < coords.Count; i++)
            {
                _structure.AddCell(coords[i], isAnchor, block.Material == BlockMaterial.Glass);
                _blocks[coords[i]] = block;
            }
            InitialCellCount += coords.Count;
        }

        private void Start()
        {
            // Runs after a code-driven spawner has registered every block; if nothing
            // did, this is a hand-authored building — adopt the children.
            if (autoScanChildren && _structure.CellCount == 0)
                ScanChildren();

            _baseline = new Dictionary<Vector2Int, CellStress>();
            foreach (var kv in _structure.ComputeStress(stressScanRange, columnLoadCapacity, columnMomentCapacity))
                _baseline[kv.Key] = kv.Value;
        }

        private void Update()
        {
            if (_crumbling.Count == 0)
            {
                _crumbleTimer = 0f;
                return;
            }

            _crumbleTimer += Time.deltaTime;
            if (_crumbleTimer < crumbleTickInterval)
                return;
            float elapsed = _crumbleTimer;
            _crumbleTimer = 0f;

            // Damage through the normal destruction pipeline, off a snapshot: a death
            // mid-loop re-enters OnBlockDestroyed and rebuilds _crumbling.
            _crumbleScratch.Clear();
            foreach (var kv in _crumbling)
                _crumbleScratch.Add(kv);
            for (int i = 0; i < _crumbleScratch.Count; i++)
            {
                BuildingBlock block = _crumbleScratch[i].Key;
                if (block == null || block.IsFalling || block.IsRubble)
                    continue;
                float excess = _crumbleScratch[i].Value - overloadTolerance;
                if (excess > 0f)
                    block.TakeDamage(excess * crumbleDamageRate * elapsed);
            }
        }

        // ---------------- Hand-authored building scan ----------------

        private struct ScanEntry
        {
            public BuildingBlock Block;
            public Bounds Aabb;
            public bool Background;
        }

        private void ScanChildren()
        {
            var entries = new List<ScanEntry>();
            foreach (var block in GetComponentsInChildren<BuildingBlock>())
            {
                if (block.IsRegistered)
                    continue;
                // Nested buildings own their own blocks.
                if (block.GetComponentInParent<DestructibleBuilding>() != this)
                    continue;
                if (!TryGetWorldAabb(block, out Bounds aabb))
                    continue;
                entries.Add(new ScanEntry
                {
                    Block = block,
                    Aabb = aabb,
                    Background = block.name.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0,
                });
            }
            if (entries.Count == 0)
                return;

            Vector2 origin = new Vector2(float.MaxValue, float.MaxValue);
            foreach (var e in entries)
                origin = Vector2.Min(origin, e.Aabb.min);

            // Foreground and bigger pieces claim their cells first so overlapping
            // background dressing can't steal a structural cell from a wall.
            entries.Sort((a, b) =>
            {
                if (a.Background != b.Background) return a.Background ? 1 : -1;
                float areaA = a.Aabb.size.x * a.Aabb.size.y;
                float areaB = b.Aabb.size.x * b.Aabb.size.y;
                if (!Mathf.Approximately(areaA, areaB)) return areaB.CompareTo(areaA);
                if (a.Aabb.min.y != b.Aabb.min.y) return a.Aabb.min.y.CompareTo(b.Aabb.min.y);
                return a.Aabb.min.x.CompareTo(b.Aabb.min.x);
            });

            var claimed = new Dictionary<Vector2Int, BuildingBlock>();
            var cellsOf = new Dictionary<BuildingBlock, List<Vector2Int>>();
            var riders = new List<(BuildingBlock rider, BuildingBlock host)>();

            foreach (var e in entries)
            {
                // Quantize: whole-cell footprint size, min-corner rounded onto the grid.
                // Hand-placed rows drift a few % per block; rounding against a shared
                // origin keeps touching blocks in adjacent cells.
                int w = Mathf.Clamp(Mathf.RoundToInt(e.Aabb.size.x / cellSize), 1, MaxFootprintCells);
                int h = Mathf.Clamp(Mathf.RoundToInt(e.Aabb.size.y / cellSize), 1, MaxFootprintCells);
                int i0 = Mathf.RoundToInt((e.Aabb.min.x - origin.x) / cellSize);
                int j0 = Mathf.RoundToInt((e.Aabb.min.y - origin.y) / cellSize);

                var cells = new List<Vector2Int>(w * h);
                BuildingBlock overlapHost = null;
                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        var c = new Vector2Int(i0 + i, j0 + j);
                        if (claimed.TryGetValue(c, out var owner))
                        {
                            overlapHost = owner;
                            continue;
                        }
                        claimed[c] = e.Block;
                        cells.Add(c);
                    }
                }

                if (cells.Count > 0)
                    cellsOf[e.Block] = cells;
                else if (overlapHost != null)
                    riders.Add((e.Block, overlapHost));
            }

            // Anchors: blocks with terrain (a non-building, non-dynamic collider)
            // directly underneath. If nothing at all touches ground — a floating or
            // mid-air arrangement — fall back to anchoring the lowest row so the
            // building still stands until hit.
            Physics2D.SyncTransforms();
            var anchored = new HashSet<BuildingBlock>();
            foreach (var kv in cellsOf)
            {
                if (TryGetWorldAabb(kv.Key, out Bounds aabb) && HasGroundBelow(aabb))
                    anchored.Add(kv.Key);
            }
            if (anchored.Count == 0)
            {
                int minJ = int.MaxValue;
                foreach (var kv in cellsOf)
                    for (int i = 0; i < kv.Value.Count; i++)
                        minJ = Mathf.Min(minJ, kv.Value[i].y);
                foreach (var kv in cellsOf)
                    for (int i = 0; i < kv.Value.Count; i++)
                        if (kv.Value[i].y == minJ)
                        {
                            anchored.Add(kv.Key);
                            break;
                        }
            }

            foreach (var kv in cellsOf)
            {
                kv.Key.Initialise(this, kv.Value);
                RegisterBlock(kv.Key, kv.Value, anchored.Contains(kv.Key));
            }

            // Riders stay outside the cell bookkeeping entirely (they are never in
            // StandingBlockCount), so they must not inflate InitialCellCount either —
            // otherwise a pristine building already reads as partially destroyed.
            foreach (var (rider, host) in riders)
            {
                rider.Initialise(this, null);
                if (!_riders.TryGetValue(host, out var list))
                    _riders[host] = list = new List<BuildingBlock>();
                list.Add(rider);
            }
        }

        private readonly Collider2D[] _probeHits = new Collider2D[16];

        private bool HasGroundBelow(Bounds aabb)
        {
            var centre = new Vector2(aabb.center.x, aabb.min.y - anchorProbeDepth * 0.5f);
            var size = new Vector2(Mathf.Max(0.1f, aabb.size.x * 0.9f), anchorProbeDepth);
            ContactFilter2D filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;

            int count = Physics2D.OverlapBox(centre, size, 0f, filter, _probeHits);
            for (int i = 0; i < count; i++)
            {
                var hit = _probeHits[i];
                if (hit == null || hit.isTrigger)
                    continue;
                if (hit.GetComponentInParent<BuildingBlock>() != null)
                    continue; // another block (this or a neighbouring building) is not ground
                var rb = hit.attachedRigidbody;
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                    continue; // players/debris don't count as ground
                return true;
            }
            return false;
        }

        /// <summary>
        /// World-space AABB of the block's solid colliders, computed from collider
        /// geometry (not <c>Collider2D.bounds</c>, which is stale before the first
        /// physics step). Handles rotated blocks — the platforms are 1×2 boxes laid
        /// sideways. Falls back to the sprite bounds for exotic collider types.
        /// </summary>
        private static bool TryGetWorldAabb(BuildingBlock block, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (var col in block.GetComponentsInChildren<Collider2D>())
            {
                if (col.isTrigger)
                    continue;
                if (!TryColliderAabb(col, out Bounds b))
                    continue;
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            if (!has)
            {
                var sr = block.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) { bounds = sr.bounds; has = true; }
            }
            return has && bounds.size.x > 0.001f && bounds.size.y > 0.001f;
        }

        private static bool TryColliderAabb(Collider2D col, out Bounds bounds)
        {
            Transform t = col.transform;
            if (col is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                bounds = new Bounds(t.TransformPoint(box.offset + new Vector2(-half.x, -half.y)), Vector3.zero);
                bounds.Encapsulate(t.TransformPoint(box.offset + new Vector2(half.x, -half.y)));
                bounds.Encapsulate(t.TransformPoint(box.offset + new Vector2(-half.x, half.y)));
                bounds.Encapsulate(t.TransformPoint(box.offset + new Vector2(half.x, half.y)));
                return true;
            }
            if (col is PolygonCollider2D poly)
            {
                bool any = false;
                bounds = default;
                for (int p = 0; p < poly.pathCount; p++)
                {
                    foreach (var point in poly.GetPath(p))
                    {
                        Vector3 wp = t.TransformPoint(point + poly.offset);
                        if (!any) { bounds = new Bounds(wp, Vector3.zero); any = true; }
                        else bounds.Encapsulate(wp);
                    }
                }
                return any;
            }
            if (col is CapsuleCollider2D capsule)
            {
                Vector2 half = capsule.size * 0.5f;
                bounds = new Bounds(t.TransformPoint(capsule.offset + new Vector2(-half.x, -half.y)), Vector3.zero);
                bounds.Encapsulate(t.TransformPoint(capsule.offset + new Vector2(half.x, half.y)));
                return true;
            }
            bounds = col.bounds; // may be stale pre-physics, but better than nothing
            return bounds.size.x > 0.001f;
        }

        // ---------------- Destruction ----------------

        /// <summary>Called by a <see cref="BuildingBlock"/> when its health reaches zero.</summary>
        public void OnBlockDestroyed(BuildingBlock block)
        {
            ReleaseRiders(block);

            if (block.Coords.Count == 0)
            {
                // A cosmetic rider died on its own — no structural consequence.
                RemoveRiderMembership(block);
                return;
            }

            for (int i = 0; i < block.Coords.Count; i++)
                _blocks.Remove(block.Coords[i]);

            List<Vector2Int> detached = _structure.RemoveCells(block.Coords);

            // Group detached cells into distinct blocks (a rigid block's cells always
            // detach together).
            var fallers = new List<BuildingBlock>();
            for (int i = 0; i < detached.Count; i++)
            {
                if (!_blocks.TryGetValue(detached[i], out var detachedBlock))
                    continue;
                _blocks.Remove(detached[i]);
                if (!fallers.Contains(detachedBlock))
                    fallers.Add(detachedBlock);
            }

            int fallen = 0;
            for (int i = 0; i < fallers.Count; i++)
            {
                ReleaseRiders(fallers[i]);
                if (fallen < maxSimultaneousFalling)
                {
                    fallers[i].BeginFalling();
                    fallen++;
                }
                else
                {
                    // Over the cap: drop it without adding a dynamic body.
                    Destroy(fallers[i].gameObject);
                }
            }

            RefreshStressVisuals();
        }

        private void ReleaseRiders(BuildingBlock host)
        {
            if (!_riders.TryGetValue(host, out var list))
                return;
            _riders.Remove(host);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    list[i].BeginFalling();
            }
        }

        private void RemoveRiderMembership(BuildingBlock rider)
        {
            foreach (var kv in _riders)
            {
                if (kv.Value.Remove(rider))
                    return;
            }
        }

        /// <summary>
        /// Recompute per-cell stress, push lean/sag targets to the surviving blocks, and
        /// collect which supports are now overloaded past their baseline enough to
        /// crumble. A multi-cell block shows its worst cell's stress. Fully deterministic
        /// from the structure state, so networked clients can run it locally with no
        /// extra sync.
        /// </summary>
        private void RefreshStressVisuals()
        {
            Dictionary<Vector2Int, CellStress> stress =
                _structure.ComputeStress(stressScanRange, columnLoadCapacity, columnMomentCapacity);

            var worst = new Dictionary<BuildingBlock, CellStress>();
            foreach (var kv in _blocks)
            {
                float s = 0f, lean = 0f, drift = 0f, over = 0f;
                if (stress.TryGetValue(kv.Key, out CellStress cs))
                {
                    s = cs.Stress;
                    lean = cs.Lean;
                    drift = cs.Drift;
                    over = cs.Overload;
                }
                if (_baseline != null && _baseline.TryGetValue(kv.Key, out CellStress baseline))
                {
                    s = Mathf.Max(0f, s - baseline.Stress);
                    over = Mathf.Max(0f, over - baseline.Overload);
                }
                // A support pushed past capacity must read as stressed (tremble) even
                // when its clamped stress was already saturated at baseline.
                s = Mathf.Max(s, Mathf.Min(1f, over));

                worst.TryGetValue(kv.Value, out CellStress current);
                if (s > current.Stress)
                {
                    current.Stress = s;
                    current.Lean = lean;
                    current.Drift = drift;
                }
                current.Overload = Mathf.Max(current.Overload, over);
                worst[kv.Value] = current;
            }

            _crumbling.Clear();
            foreach (var kv in worst)
            {
                if (kv.Value.Overload > overloadTolerance)
                    _crumbling[kv.Key] = kv.Value.Overload;

                kv.Key.SetStress(kv.Value.Stress, kv.Value.Lean, kv.Value.Drift, kv.Value.Overload);

                // Background riders mirror their host so a wall and its dressing
                // lean as one.
                if (_riders.TryGetValue(kv.Key, out var riderList))
                    for (int i = 0; i < riderList.Count; i++)
                        if (riderList[i] != null)
                            riderList[i].SetStress(kv.Value.Stress, kv.Value.Lean, kv.Value.Drift, kv.Value.Overload);
            }
        }
    }
}
