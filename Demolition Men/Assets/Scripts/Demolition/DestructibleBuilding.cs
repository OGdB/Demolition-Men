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
    /// This is where the discrete, deterministic "which cells exist / which detached"
    /// state lives — the exact thing that would be replicated over Fishnet (see the
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

        private readonly BuildingStructure _structure = new BuildingStructure();
        private readonly Dictionary<Vector2Int, BuildingBlock> _blocks =
            new Dictionary<Vector2Int, BuildingBlock>();

        // Stress present in the intact building (e.g. a roof over a hollow interior).
        // Only damage-induced stress beyond this baseline is shown to the player.
        private Dictionary<Vector2Int, float> _baselineStress;

        public int StandingBlockCount => _structure.CellCount;
        public int InitialBlockCount { get; private set; }

        /// <summary>Fraction of the building destroyed or collapsed, 0..1.</summary>
        public float DestroyedFraction =>
            InitialBlockCount == 0 ? 0f : 1f - (float)StandingBlockCount / InitialBlockCount;

        /// <summary>Register a freshly-spawned block with the structure.</summary>
        public void RegisterBlock(BuildingBlock block, Vector2Int coord, bool isAnchor)
        {
            _structure.AddCell(coord, isAnchor);
            _blocks[coord] = block;
            InitialBlockCount++;
        }

        private void Start()
        {
            // Runs after the bootstrap/spawner has registered every block.
            _baselineStress = new Dictionary<Vector2Int, float>();
            foreach (var kv in _structure.ComputeStress(stressScanRange, columnLoadCapacity, columnMomentCapacity))
                _baselineStress[kv.Key] = kv.Value.Stress;
        }

        /// <summary>Called by a <see cref="BuildingBlock"/> when its health reaches zero.</summary>
        public void OnBlockDestroyed(BuildingBlock block)
        {
            var coord = block.Coord;
            _blocks.Remove(coord);

            List<Vector2Int> detached = _structure.RemoveCell(coord);

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

            RefreshStressVisuals();
        }

        /// <summary>
        /// Recompute per-cell stress and push lean/sag targets to the surviving blocks.
        /// Purely cosmetic and fully deterministic from the structure state, so networked
        /// clients can run it locally with no extra sync.
        /// </summary>
        private void RefreshStressVisuals()
        {
            Dictionary<Vector2Int, CellStress> stress =
                _structure.ComputeStress(stressScanRange, columnLoadCapacity, columnMomentCapacity);
            foreach (var kv in _blocks)
            {
                float s = 0f, lean = 0f, drift = 0f;
                if (stress.TryGetValue(kv.Key, out CellStress cs))
                {
                    s = cs.Stress;
                    lean = cs.Lean;
                    drift = cs.Drift;
                }
                if (_baselineStress != null && _baselineStress.TryGetValue(kv.Key, out float baseline))
                    s = Mathf.Max(0f, s - baseline);

                kv.Value.SetStress(s, lean, drift);
            }
        }
    }
}
