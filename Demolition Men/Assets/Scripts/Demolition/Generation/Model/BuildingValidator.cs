using System.Collections.Generic;
using Demolition.Core;
using UnityEngine;

namespace Demolition.Generation
{
    public sealed class BuildingReport
    {
        public int Cells;
        /// <summary>Cells with no path to the ground at spawn (must be 0).</summary>
        public int UnsupportedCells;
        /// <summary>Ground-level blocks whose loss brings down at least the archetype's cascade share.</summary>
        public readonly List<SpecBlock> WeakPoints = new List<SpecBlock>();
        /// <summary>Largest share of the building one ground-level block was seen to bring down.</summary>
        public float MaxCascade;
        public bool MeetsTargets;
        public int Attempts = 1;

        public bool Supported => UnsupportedCells == 0;

        public override string ToString() =>
            $"cells {Cells}, unsupported {UnsupportedCells}, weak points {WeakPoints.Count}, " +
            $"max cascade {MaxCascade:P0}, attempts {Attempts}, {(MeetsTargets ? "OK" : "FAIL")}";
    }

    /// <summary>
    /// Judges a spec with the rules it will play by: the same support graph, stress
    /// heuristic and overload crumble as <see cref="DestructibleBuilding"/>. A weak point
    /// is a ground-level block whose destruction (plus everything that then detaches
    /// or crumbles) takes down a big share of the building — the "hit it there" spot
    /// that makes demolition feel smart.
    /// </summary>
    public static class BuildingValidator
    {
        /// <summary>A crumble only counts toward a weak point if it happens this soon after
        /// the previous loss; slower creaking isn't a collapse the player will notice.</summary>
        public const float CrumbleWindowSeconds = 3f;

        public static BuildingReport Validate(BuildingSpec spec, BuildingArchetype archetype)
        {
            var report = new BuildingReport { Cells = spec.CellCount };
            BuildingStructure structure = spec.ToStructure();
            report.UnsupportedCells = spec.CellCount - structure.ComputeSupported().Count;
            if (!report.Supported)
                return report;

            Dictionary<Vector2Int, CellStress> baseline = Stress(structure);
            foreach (SpecBlock block in spec.Blocks)
            {
                if (block.Rect.yMin > 1 || block.Material == BlockMaterial.Glass)
                    continue;
                float lost = Cascade(spec, baseline, block, archetype.weakPointCascade);
                report.MaxCascade = Mathf.Max(report.MaxCascade, lost);
                if (lost >= archetype.weakPointCascade)
                    report.WeakPoints.Add(block);
            }
            report.MeetsTargets = archetype.weakPoints.Contains(report.WeakPoints.Count);
            return report;
        }

        /// <summary>
        /// Share of the building's cells lost when <paramref name="first"/> is destroyed:
        /// its own cells, whatever detaches, and every support that then crumbles under
        /// overload within <see cref="CrumbleWindowSeconds"/> — one at a time, quickest to
        /// give way first, as in play.
        /// </summary>
        /// <param name="stopAt">Stop once this share is lost (enough to call it a weak point).</param>
        public static float Cascade(BuildingSpec spec, Dictionary<Vector2Int, CellStress> baseline, SpecBlock first, float stopAt = 1f)
        {
            BuildingStructure structure = spec.ToStructure();
            float total = spec.CellCount;
            int lost = 0;
            var cells = new List<Vector2Int>();
            for (SpecBlock next = first; next != null && lost / total < stopAt;)
            {
                cells.Clear();
                cells.AddRange(next.Cells());
                int before = structure.CellCount;
                structure.RemoveCells(cells);
                lost += before - structure.CellCount;
                next = FirstToCrumble(spec, structure, baseline);
            }
            return lost / total;
        }

        public static Dictionary<Vector2Int, CellStress> Stress(BuildingStructure structure) =>
            structure.ComputeStress(StructureDefaults.StressScanRange, StructureDefaults.ColumnLoadCapacity, StructureDefaults.ColumnMomentCapacity);

        // The overloaded block that dies first under crumble damage (within the window), or null.
        private static SpecBlock FirstToCrumble(BuildingSpec spec, BuildingStructure structure, Dictionary<Vector2Int, CellStress> baseline)
        {
            var overload = new Dictionary<SpecBlock, float>();
            foreach (KeyValuePair<Vector2Int, CellStress> kv in Stress(structure))
            {
                SpecBlock block = spec.At(kv.Key);
                if (block == null)
                    continue;
                float over = kv.Value.Overload;
                if (baseline.TryGetValue(kv.Key, out CellStress b))
                    over = Mathf.Max(0f, over - b.Overload);
                overload[block] = overload.TryGetValue(block, out float current) ? Mathf.Max(current, over) : over;
            }

            SpecBlock first = null;
            float soonest = CrumbleWindowSeconds;
            foreach (KeyValuePair<SpecBlock, float> kv in overload)
            {
                float excess = kv.Value - StructureDefaults.OverloadTolerance;
                if (excess <= 0f)
                    continue;
                float timeToDie = BlockMaterialInfo.DefaultHealth(kv.Key.Material) / (excess * StructureDefaults.CrumbleDamageRate);
                if (timeToDie < soonest)
                {
                    soonest = timeToDie;
                    first = kv.Key;
                }
            }
            return first;
        }
    }
}
