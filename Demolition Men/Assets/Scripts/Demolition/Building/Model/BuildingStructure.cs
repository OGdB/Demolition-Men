using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Core
{
    /// <summary>
    /// Cosmetic stress of one cell.
    /// <see cref="Stress"/>: how precarious/overloaded it is (0 = solid → 1 = at its limit).
    /// <see cref="Lean"/>: which way it tends to tip (-1 = clockwise, i.e. its support or
    /// its load is to the left/right respectively; +1 = counter-clockwise; 0 = level).
    /// <see cref="Drift"/>: normalized sideways displacement for bending columns
    /// (negative = left), growing with height so a loaded column bows instead of tilting
    /// as a rigid stick; 0 for horizontally-carried cells.
    /// <see cref="Overload"/>: load-to-capacity ratio, UNCLAMPED — <see cref="Stress"/>
    /// is this saturated at 1, so a column carrying three buildings' worth of weight
    /// reads 3, not 1. Callers use it to decide whether a support should give way.
    /// </summary>
    public struct CellStress
    {
        public float Stress;
        public float Lean;
        public float Drift;
        public float Overload;
    }

    /// <summary>
    /// Pure (engine-light) structural model for one building, implementing the
    /// "support-graph" of Option C.
    ///
    /// A building is a set of grid cells. Some cells are <b>anchors</b> (the
    /// foundation row / load-bearing supports that count as "connected to ground").
    /// A cell is <b>supported</b> if a path of intact cells (4-neighbour) connects it
    /// to any anchor. When a cell is destroyed, every cell that loses its path to an
    /// anchor becomes <b>detached</b> and must fall.
    ///
    /// Cells may be <b>fragile</b> (glass panes): they hang in their frame — supported
    /// while any structural neighbour is — but have no carrying capacity, so support
    /// never flows <i>through</i> them, they never count as pillars, and they transmit
    /// no span loads.
    ///
    /// This class contains NO physics and NO MonoBehaviour code, so it can be unit
    /// tested headlessly. <see cref="Demolition.DestructibleBuilding"/> drives the
    /// Unity side from it.
    /// </summary>
    public class BuildingStructure
    {
        private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _anchors = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _fragile = new HashSet<Vector2Int>();

        // Scratch buffers reused across calls to avoid per-destruction GC.
        private readonly HashSet<Vector2Int> _supportedScratch = new HashSet<Vector2Int>();
        private readonly Queue<Vector2Int> _bfsQueue = new Queue<Vector2Int>();

        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        public IReadOnlyCollection<Vector2Int> Cells => _cells;
        public int CellCount => _cells.Count;

        public void AddCell(Vector2Int coord, bool isAnchor = false, bool isFragile = false)
        {
            _cells.Add(coord);
            if (isAnchor)
                _anchors.Add(coord);
            if (isFragile)
                _fragile.Add(coord);
        }

        public bool HasCell(Vector2Int coord) => _cells.Contains(coord);
        public bool IsAnchor(Vector2Int coord) => _anchors.Contains(coord);
        public bool IsFragile(Vector2Int coord) => _fragile.Contains(coord);

        /// <summary>
        /// Remove a destroyed cell and return every cell that became detached as a
        /// result (the destroyed cell itself is NOT included). Detached cells are
        /// removed from the structure too, since once they fall they are no longer
        /// load-bearing.
        /// </summary>
        public List<Vector2Int> RemoveCell(Vector2Int coord)
        {
            _singleCellScratch[0] = coord;
            return RemoveCells(_singleCellScratch);
        }

        private readonly Vector2Int[] _singleCellScratch = new Vector2Int[1];

        /// <summary>
        /// Remove several cells atomically (a destroyed multi-cell block) and return
        /// every OTHER cell that became detached as a result. One flood-fill total, so
        /// a rigid block spanning many cells can't tear itself apart halfway through.
        /// </summary>
        public List<Vector2Int> RemoveCells(IReadOnlyList<Vector2Int> coords)
        {
            var detached = new List<Vector2Int>();
            bool removedAny = false;
            for (int i = 0; i < coords.Count; i++)
            {
                if (!_cells.Remove(coords[i]))
                    continue;
                removedAny = true;
                _anchors.Remove(coords[i]);
                _fragile.Remove(coords[i]);
            }

            if (!removedAny)
                return detached; // nothing to do

            // No point flood-filling if the whole thing is already gone.
            if (_cells.Count == 0)
                return detached;

            ComputeSupportedInto(_supportedScratch);

            foreach (var cell in _cells)
            {
                if (!_supportedScratch.Contains(cell))
                    detached.Add(cell);
            }

            for (int i = 0; i < detached.Count; i++)
            {
                _cells.Remove(detached[i]);
                _anchors.Remove(detached[i]);
                _fragile.Remove(detached[i]);
            }

            return detached;
        }

        /// <summary>
        /// <see cref="RemoveCells(IReadOnlyList{Vector2Int})"/>, and a block that lost a cell leaves
        /// whole: a pane holds on by each of its own cells, so one can detach while the other still
        /// hangs from its frame, but the block falls as one. <paramref name="blockOf"/> gives the
        /// cells of the block on a cell, or null. The game and the ranked replay both go through
        /// here, so they agree on what a collapse took.
        /// </summary>
        public List<Vector2Int> RemoveCells(IReadOnlyList<Vector2Int> coords, Func<Vector2Int, IEnumerable<Vector2Int>> blockOf)
        {
            List<Vector2Int> detached = RemoveCells(coords);
            for (int i = 0; i < detached.Count; i++)
            {
                IEnumerable<Vector2Int> block = blockOf(detached[i]);
                if (block == null)
                    continue;
                foreach (Vector2Int cell in block)
                    if (_cells.Contains(cell))
                    {
                        detached.Add(cell);
                        detached.AddRange(RemoveCells(new[] { cell }));
                    }
            }
            return detached;
        }

        /// <summary>
        /// Remove and return every cell with no path to an anchor right now (no cell
        /// destroyed). For a freshly built structure: a hand-placed block that doesn't
        /// touch the rest would otherwise hang in the air until the first hit.
        /// </summary>
        public List<Vector2Int> RemoveUnsupported()
        {
            var detached = new List<Vector2Int>();
            ComputeSupportedInto(_supportedScratch);
            foreach (var cell in _cells)
                if (!_supportedScratch.Contains(cell))
                    detached.Add(cell);
            for (int i = 0; i < detached.Count; i++)
            {
                _cells.Remove(detached[i]);
                _anchors.Remove(detached[i]);
                _fragile.Remove(detached[i]);
            }
            return detached;
        }

        /// <summary>All cells currently connected to an anchor (BFS over 4-neighbours).</summary>
        public HashSet<Vector2Int> ComputeSupported()
        {
            var result = new HashSet<Vector2Int>();
            ComputeSupportedInto(result);
            return result;
        }

        // ---------- Stress heuristic (pre-collapse lean/sag visuals) ----------

        /// <summary>
        /// <see cref="CellStress.Overload"/> assigned to a cell held only through a
        /// roundabout path (no pillar within scan range): well past capacity, so a
        /// caller that fails overloaded supports will let such orphaned chunks give way.
        /// </summary>
        public const float UnsupportedOverload = 1.5f;

        /// <summary>
        /// Cells with a continuous vertical run of intact cells straight down to an
        /// anchor. These carry load "columnar" and feel no stress. Fragile cells end a
        /// run — a pillar cannot continue up through a glass pane.
        /// </summary>
        public HashSet<Vector2Int> ComputePillared()
        {
            var pillared = new HashSet<Vector2Int>();
            foreach (var anchor in _anchors)
            {
                var c = anchor;
                while (_cells.Contains(c) && !_fragile.Contains(c) && pillared.Add(c))
                    c += Vector2Int.up;
            }
            return pillared;
        }

        /// <summary>
        /// Heuristic per-cell stress for visual anticipation, in two passes.
        ///
        /// Pass 1 — carried cells (not pillared) are held sideways: scan along the row
        /// (through contiguous cells only) for the nearest pillared cell on each side.
        /// One-sided support is a cantilever — stress grows with overhang length and the
        /// cell leans away from its support; both sides is a span — worst at mid-span,
        /// level there. Each carried cell also *attributes its weight* to the pillar
        /// cell(s) holding it (split by proximity), with the lever arm as a signed
        /// bending moment.
        ///
        /// Pass 2 — pillar columns: each contiguous vertical run sums the load and
        /// moment that entered anywhere along it. Heavy load ⇒ stress (an overloaded
        /// column trembles even when balanced); net moment ⇒ the column leans toward
        /// the mass it carries, with a height² <see cref="CellStress.Drift"/> so it
        /// visibly bows.
        ///
        /// Purely cosmetic: collapse decisions remain <see cref="RemoveCell"/>'s
        /// connectivity check, and callers must never move colliders from this.
        /// </summary>
        /// <param name="maxCantilever">Row-scan range for finding a supporting pillar.</param>
        /// <param name="loadCapacity">Carried-weight shares at which a column's stress saturates.</param>
        /// <param name="momentCapacity">Net lever-arm sum at which a column's lean saturates.</param>
        public Dictionary<Vector2Int, CellStress> ComputeStress(
            int maxCantilever = 4, float loadCapacity = 4f, float momentCapacity = 8f)
        {
            HashSet<Vector2Int> pillared = ComputePillared();
            var result = new Dictionary<Vector2Int, CellStress>(_cells.Count);
            var load = new Dictionary<Vector2Int, float>();   // weight shares entering each pillar cell
            var moment = new Dictionary<Vector2Int, float>(); // signed lever sum (+ = load to the left = CCW)

            // ---- Pass 1: carried cells + load attribution ----
            foreach (var cell in _cells)
            {
                if (_fragile.Contains(cell))
                {
                    // Panes just hang in their frame — they carry nothing, add no
                    // load to any pillar, and show no strain of their own.
                    result[cell] = new CellStress();
                    continue;
                }

                if (pillared.Contains(cell))
                    continue; // handled in pass 2

                int dL = DistanceToPillar(cell, -1, maxCantilever, pillared);
                int dR = DistanceToPillar(cell, +1, maxCantilever, pillared);
                bool hasL = dL > 0, hasR = dR > 0;

                float stress, lean, overload;
                if (hasL && hasR)
                {
                    // Simply-supported span: sags most at mid-span, stays level there.
                    stress = 0.7f * Mathf.Min(1f, Mathf.Min(dL, dR) / (float)maxCantilever);
                    lean = (dL - dR) / (float)(dL + dR);
                    overload = stress; // a proper span never exceeds capacity by itself

                    // The nearer pillar carries the bigger share.
                    float shareL = dR / (float)(dL + dR);
                    float shareR = 1f - shareL;
                    Attribute(load, moment, new Vector2Int(cell.x - dL, cell.y), shareL, -dL * shareL);
                    Attribute(load, moment, new Vector2Int(cell.x + dR, cell.y), shareR, +dR * shareR);
                }
                else if (hasL)
                {
                    stress = Mathf.Min(1f, dL / (float)maxCantilever);
                    lean = -1f; // supported from the left → tips clockwise over the gap
                    overload = stress;
                    Attribute(load, moment, new Vector2Int(cell.x - dL, cell.y), 1f, -dL);
                }
                else if (hasR)
                {
                    stress = Mathf.Min(1f, dR / (float)maxCantilever);
                    lean = 1f;
                    overload = stress;
                    Attribute(load, moment, new Vector2Int(cell.x + dR, cell.y), 1f, +dR);
                }
                else
                {
                    stress = 1f; // held only via a roundabout path — about to give
                    lean = 0f;
                    overload = UnsupportedOverload;
                }

                result[cell] = new CellStress { Stress = stress, Lean = lean, Drift = 0f, Overload = overload };
            }

            // ---- Pass 2: pillar columns bear what was attributed to them ----
            foreach (List<Vector2Int> run in ContiguousPillarRuns(pillared))
            {
                float runLoad = 0f, runMoment = 0f;
                for (int i = 0; i < run.Count; i++)
                {
                    if (load.TryGetValue(run[i], out float l)) runLoad += l;
                    if (moment.TryGetValue(run[i], out float m)) runMoment += m;
                }

                float overload = runLoad / loadCapacity;
                float stress = Mathf.Min(1f, overload);
                float lean = Mathf.Clamp(runMoment / momentCapacity, -1f, 1f);

                int baseY = run[0].y; // runs are built bottom-up
                for (int i = 0; i < run.Count; i++)
                {
                    float h = run.Count <= 1 ? 0f : (run[i].y - baseY) / (float)(run.Count - 1);
                    result[run[i]] = new CellStress
                    {
                        Stress = stress,
                        Lean = lean,
                        // CCW lean (+) means the column bows to the LEFT (negative x),
                        // displacement growing with height² like a bending beam.
                        Drift = -lean * h * h,
                        Overload = overload,
                    };
                }
            }

            return result;
        }

        private static void Attribute(Dictionary<Vector2Int, float> load,
            Dictionary<Vector2Int, float> moment, Vector2Int pillarCell, float share, float lever)
        {
            load.TryGetValue(pillarCell, out float l);
            load[pillarCell] = l + share;
            moment.TryGetValue(pillarCell, out float m);
            moment[pillarCell] = m + lever;
        }

        /// <summary>Group pillared cells into contiguous vertical runs (per x, bottom-up).</summary>
        private static List<List<Vector2Int>> ContiguousPillarRuns(HashSet<Vector2Int> pillared)
        {
            var byColumn = new Dictionary<int, List<int>>();
            foreach (var cell in pillared)
            {
                if (!byColumn.TryGetValue(cell.x, out var ys))
                    byColumn[cell.x] = ys = new List<int>();
                ys.Add(cell.y);
            }

            var runs = new List<List<Vector2Int>>();
            foreach (var kv in byColumn)
            {
                kv.Value.Sort();
                List<Vector2Int> current = null;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (current == null || kv.Value[i] != kv.Value[i - 1] + 1)
                        runs.Add(current = new List<Vector2Int>());
                    current.Add(new Vector2Int(kv.Key, kv.Value[i]));
                }
            }
            return runs;
        }

        private int DistanceToPillar(Vector2Int cell, int dirX, int maxSteps, HashSet<Vector2Int> pillared)
        {
            var c = cell;
            for (int step = 1; step <= maxSteps; step++)
            {
                c = new Vector2Int(c.x + dirX, c.y);
                if (!_cells.Contains(c) || _fragile.Contains(c))
                    return -1; // gap (or a pane, which transmits no load) — no path this way
                if (pillared.Contains(c))
                    return step;
            }
            return -1;
        }

        private void ComputeSupportedInto(HashSet<Vector2Int> supported)
        {
            supported.Clear();
            _bfsQueue.Clear();

            foreach (var anchor in _anchors)
            {
                if (!_cells.Contains(anchor) || !supported.Add(anchor))
                    continue;
                if (!_fragile.Contains(anchor))
                    _bfsQueue.Enqueue(anchor);
            }

            while (_bfsQueue.Count > 0)
            {
                var current = _bfsQueue.Dequeue();
                for (int i = 0; i < Neighbours.Length; i++)
                {
                    var next = current + Neighbours[i];
                    if (!_cells.Contains(next) || !supported.Add(next))
                        continue;
                    // A fragile cell is held by its frame, but support never flows
                    // through it — nothing may rest on glass.
                    if (!_fragile.Contains(next))
                        _bfsQueue.Enqueue(next);
                }
            }
        }
    }
}
