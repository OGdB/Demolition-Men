using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Core
{
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
