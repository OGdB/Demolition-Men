using System.Collections.Generic;
using Demolition.Generation;

namespace Demolition.Levels
{
    public enum SegmentKind { Start, Street, Cluster, Tower, Gap, Hill, Sewer, Gate, Breather, Finish }

    /// <summary>Columns [X0, X1) of the level built from one pacing token; the unit the
    /// builder streams in and out.</summary>
    public sealed class LevelSegment
    {
        public SegmentKind Kind;
        public int X0;
        public int X1;
    }

    /// <summary>A building standing with its bottom-left cell on column <see cref="X"/>,
    /// its ground row on top of the ground at <see cref="GroundY"/>.</summary>
    public sealed class BuildingPlacement
    {
        public int X;
        public int GroundY;
        public BuildingSpec Spec;
        public int Segment;
    }

    public sealed class PickupPlacement
    {
        public float X;
        public float Y;
        public PickupKind Kind;
        public int Amount;
        public int Segment;
    }

    /// <summary>
    /// A generated level as data: the ground height of every column, the buildings and
    /// pickups on it, where players start and where they finish. Columns are 1 unit wide;
    /// column x spans world x..x+1 relative to the level origin.
    /// </summary>
    public sealed class LevelLayout
    {
        /// <summary>Ground height of a column with no ground (a pit into the void).</summary>
        public const int NoGround = int.MinValue;

        public LevelLayout(LevelSpec spec) => Spec = spec;

        public LevelSpec Spec { get; }
        /// <summary>Top surface of each column: the ground is solid below this y.</summary>
        public List<int> Ground { get; } = new List<int>();
        public List<LevelSegment> Segments { get; } = new List<LevelSegment>();
        public List<BuildingPlacement> Buildings { get; } = new List<BuildingPlacement>();
        public List<PickupPlacement> Pickups { get; } = new List<PickupPlacement>();
        public int SpawnX { get; set; }
        public int FinishX { get; set; }
        /// <summary>How many times the grammar re-rolled to pass the traversal check.</summary>
        public int Attempts { get; set; } = 1;

        public int Width => Ground.Count;

        public int GroundAt(int x) => x >= 0 && x < Ground.Count ? Ground[x] : NoGround;

        /// <summary>Destructible cells over the whole level (the demolition meter's 100%).</summary>
        public int TotalCells
        {
            get
            {
                int cells = 0;
                foreach (BuildingPlacement b in Buildings)
                    cells += b.Spec.CellCount;
                return cells;
            }
        }

        /// <summary>Lowest ground in the level (the camera's deepest framing).</summary>
        public int LowestGround
        {
            get
            {
                int low = int.MaxValue;
                foreach (int g in Ground)
                    if (g != NoGround && g < low)
                        low = g;
                return low == int.MaxValue ? 0 : low;
            }
        }

        /// <summary>Order-sensitive content hash: equal layouts hash equal.</summary>
        public int Hash()
        {
            unchecked
            {
                uint h = 2166136261;
                void Mix(int v) { h = (h ^ (uint)v) * 16777619; }
                foreach (int g in Ground) Mix(g);
                foreach (BuildingPlacement b in Buildings) { Mix(b.X); Mix(b.GroundY); Mix(b.Spec.Hash()); }
                foreach (PickupPlacement p in Pickups) { Mix((int)(p.X * 10f)); Mix((int)(p.Y * 10f)); Mix((int)p.Kind); Mix(p.Amount); }
                Mix(SpawnX);
                Mix(FinishX);
                return (int)h;
            }
        }
    }
}
