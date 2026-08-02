using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Core
{
    /// <summary>
    /// Cosmetic stress of one cell: how precarious it is (0 = solid → 1 = about to give)
    /// and which way it tends to tip (-1 = clockwise, i.e. its support is to the left;
    /// +1 = counter-clockwise; 0 = straight-down sag).
    /// </summary>
    public struct CellStress
    {
        public float Stress;
        public float Lean;
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
    /// This class contains NO physics and NO MonoBehaviour code, so it can be unit
    /// tested headlessly. <see cref="Demolition.DestructibleBuilding"/> drives the
    /// Unity side from it.
    /// </summary>
    public class BuildingStructure
    {
        private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _anchors = new HashSet<Vector2Int>();

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

        public void AddCell(Vector2Int coord, bool isAnchor = false)
        {
            _cells.Add(coord);
            if (isAnchor)
                _anchors.Add(coord);
        }

        public bool HasCell(Vector2Int coord) => _cells.Contains(coord);
        public bool IsAnchor(Vector2Int coord) => _anchors.Contains(coord);

        /// <summary>
        /// Remove a destroyed cell and return every cell that became detached as a
        /// result (the destroyed cell itself is NOT included). Detached cells are
        /// removed from the structure too, since once they fall they are no longer
        /// load-bearing.
        /// </summary>
        public List<Vector2Int> RemoveCell(Vector2Int coord)
        {
            var detached = new List<Vector2Int>();
            if (!_cells.Remove(coord))
                return detached; // nothing to do

            _anchors.Remove(coord);

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
        /// Cells with a continuous vertical run of intact cells straight down to an
        /// anchor. These carry load "columnar" and feel no stress.
        /// </summary>
        public HashSet<Vector2Int> ComputePillared()
        {
            var pillared = new HashSet<Vector2Int>();
            foreach (var anchor in _anchors)
            {
                var c = anchor;
                while (_cells.Contains(c) && pillared.Add(c))
                    c += Vector2Int.up;
            }
            return pillared;
        }

        /// <summary>
        /// Heuristic per-cell stress for visual anticipation. A cell that is not
        /// pillared is carried sideways: scan along its row (through contiguous cells
        /// only) for the nearest pillared cell on each side. One-sided support is a
        /// cantilever — stress grows with overhang length and the cell leans away from
        /// its support; support on both sides is a span — worst at mid-span, level
        /// there. Purely cosmetic: collapse decisions remain
        /// <see cref="RemoveCell"/>'s connectivity check, and callers must never move
        /// colliders from this.
        /// </summary>
        public Dictionary<Vector2Int, CellStress> ComputeStress(int maxCantilever = 4)
        {
            HashSet<Vector2Int> pillared = ComputePillared();
            var result = new Dictionary<Vector2Int, CellStress>(_cells.Count);

            foreach (var cell in _cells)
            {
                if (pillared.Contains(cell))
                {
                    result[cell] = default;
                    continue;
                }

                int dL = DistanceToPillar(cell, -1, maxCantilever, pillared);
                int dR = DistanceToPillar(cell, +1, maxCantilever, pillared);
                bool hasL = dL > 0, hasR = dR > 0;

                float stress, lean;
                if (hasL && hasR)
                {
                    // Simply-supported span: sags most at mid-span, stays level there.
                    stress = 0.7f * Mathf.Min(1f, Mathf.Min(dL, dR) / (float)maxCantilever);
                    lean = (dL - dR) / (float)(dL + dR);
                }
                else if (hasL)
                {
                    stress = Mathf.Min(1f, dL / (float)maxCantilever);
                    lean = -1f; // supported from the left → tips clockwise over the gap
                }
                else if (hasR)
                {
                    stress = Mathf.Min(1f, dR / (float)maxCantilever);
                    lean = 1f;
                }
                else
                {
                    stress = 1f; // held only via a roundabout path — about to give
                    lean = 0f;
                }

                result[cell] = new CellStress { Stress = stress, Lean = lean };
            }

            return result;
        }

        private int DistanceToPillar(Vector2Int cell, int dirX, int maxSteps, HashSet<Vector2Int> pillared)
        {
            var c = cell;
            for (int step = 1; step <= maxSteps; step++)
            {
                c = new Vector2Int(c.x + dirX, c.y);
                if (!_cells.Contains(c))
                    return -1; // gap — no contiguous load path this way
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
                if (_cells.Contains(anchor) && supported.Add(anchor))
                    _bfsQueue.Enqueue(anchor);
            }

            while (_bfsQueue.Count > 0)
            {
                var current = _bfsQueue.Dequeue();
                for (int i = 0; i < Neighbours.Length; i++)
                {
                    var next = current + Neighbours[i];
                    if (_cells.Contains(next) && supported.Add(next))
                        _bfsQueue.Enqueue(next);
                }
            }
        }
    }
}
