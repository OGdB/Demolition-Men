using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Destruction facts raised by the building layer for whoever keeps score (the run
    /// controller). Carries no rules of its own. Instigator is a player index, or -1 when
    /// no player caused it.
    /// </summary>
    public static class DestructionEvents
    {
        /// <summary>A building finished registering; argument is its structural cell count.</summary>
        public static event Action<int> BuildingReady;

        /// <summary>Cells left the standing structure (destroyed or detached).</summary>
        public static event Action<int> StandingCellsRemoved;

        /// <summary>A block was destroyed by damage and had not been credited by a collapse.</summary>
        public static event Action<BlockDestroyedInfo> BlockDestroyed;

        /// <summary>Destroying a block detached others from the ground.</summary>
        public static event Action<CollapseInfo> Collapsed;

        internal static void RaiseBuildingReady(int cells) => BuildingReady?.Invoke(cells);
        internal static void RaiseStandingCellsRemoved(int cells) => StandingCellsRemoved?.Invoke(cells);
        internal static void RaiseBlockDestroyed(BlockDestroyedInfo info) => BlockDestroyed?.Invoke(info);
        internal static void RaiseCollapsed(CollapseInfo info) => Collapsed?.Invoke(info);
    }

    public readonly struct BlockDestroyedInfo
    {
        public readonly BlockMaterial Material;
        /// <summary>Cells the block occupied, at least 1.</summary>
        public readonly int Cells;
        public readonly int Instigator;
        public readonly Vector3 Position;

        public BlockDestroyedInfo(BlockMaterial material, int cells, int instigator, Vector3 position)
        {
            Material = material;
            Cells = cells;
            Instigator = instigator;
            Position = position;
        }
    }

    public readonly struct CollapseInfo
    {
        public readonly int Instigator;
        /// <summary>One entry per detached cell.</summary>
        public readonly IReadOnlyList<BlockMaterial> Cells;
        public readonly Vector3 Position;

        public CollapseInfo(int instigator, IReadOnlyList<BlockMaterial> cells, Vector3 position)
        {
            Instigator = instigator;
            Cells = cells;
            Position = position;
        }
    }
}
