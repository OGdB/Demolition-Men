using System.Collections.Generic;
using Demolition.Core;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Owns one building's <see cref="BuildingStructure"/> and the live
    /// <see cref="BuildingBlock"/> instances. When a block is destroyed it re-runs the
    /// support flood-fill and tells any newly-detached blocks to fall.
    ///
    /// This is where the discrete, deterministic "which cells exist / which detached"
    /// state lives — the exact thing that would be replicated over Fishnet (see the
    /// design doc, §Networking). Physics is only ever touched for falling debris.
    /// </summary>
    public class DestructibleBuilding : MonoBehaviour
    {
        [Tooltip("Safety cap on how many blocks may convert to dynamic bodies in a single "
               + "collapse event. Extra detached blocks are removed without simulation.")]
        [SerializeField] private int maxSimultaneousFalling = 200;

        private readonly BuildingStructure _structure = new BuildingStructure();
        private readonly Dictionary<Vector2Int, BuildingBlock> _blocks =
            new Dictionary<Vector2Int, BuildingBlock>();

        public int StandingBlockCount => _structure.CellCount;

        /// <summary>Register a freshly-spawned block with the structure.</summary>
        public void RegisterBlock(BuildingBlock block, Vector2Int coord, bool isAnchor)
        {
            _structure.AddCell(coord, isAnchor);
            _blocks[coord] = block;
        }

        /// <summary>Called by a <see cref="BuildingBlock"/> when its health reaches zero.</summary>
        public void OnBlockDestroyed(BuildingBlock block)
        {
            var coord = block.Coord;
            _blocks.Remove(coord);

            List<Vector2Int> detached = _structure.RemoveCell(coord);
            if (detached.Count == 0)
                return;

            int fallen = 0;
            for (int i = 0; i < detached.Count; i++)
            {
                if (!_blocks.TryGetValue(detached[i], out var detachedBlock))
                    continue;
                _blocks.Remove(detached[i]);

                if (fallen < maxSimultaneousFalling)
                {
                    detachedBlock.BeginFalling();
                    fallen++;
                }
                else
                {
                    // Over the cap: drop it without adding a dynamic body.
                    Destroy(detachedBlock.gameObject);
                }
            }
        }
    }
}
