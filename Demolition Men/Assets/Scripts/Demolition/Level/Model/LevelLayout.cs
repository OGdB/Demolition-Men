using System.Collections.Generic;
using Demolition.Generation;

namespace Demolition.Levels
{
    public enum SegmentKind { Start, Street, Cluster, Tower, Gap, Hill, Sewer, Gate, Breather, Finish, Islands, Plateau, Terrace, Yard }

    /// <summary>Street dressing, cosmetic only; each theme draws its own (a Town lamp post,
    /// a Port dock light...).</summary>
    public enum DecorKind { Lamp, Fence, Bush, Bin, Sign }

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

        /// <summary>A street prop (crates, a car, gas tanks, a container), not a building.</summary>
        public bool IsProp => Spec.Archetype.StartsWith(LevelGrammar.PropPrefix, System.StringComparison.Ordinal);
    }

    /// <summary>One piece of street dressing standing on column <see cref="X"/>'s ground.</summary>
    public sealed class DecorPlacement
    {
        public DecorKind Kind;
        public int X;
        public int GroundY;
        public int Variant;
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
        /// <summary>Columns the crew's caravan stands on past the finish (its sprite is scaled to them).</summary>
        public const int CaravanWidth = 6;
        /// <summary>Rows of solid ground below the lowest surface, so pits and sewers have walls
        /// and a zoomed-out camera (the Clip Lab's square frames) never sees under the street.</summary>
        public const int GroundDepth = 12;

        public LevelLayout(LevelSpec spec) => Spec = spec;

        public LevelSpec Spec { get; }
        /// <summary>Top surface of each column: the ground is solid below this y.</summary>
        public List<int> Ground { get; } = new List<int>();
        public List<LevelSegment> Segments { get; } = new List<LevelSegment>();
        public List<BuildingPlacement> Buildings { get; } = new List<BuildingPlacement>();
        public List<PickupPlacement> Pickups { get; } = new List<PickupPlacement>();
        public List<DecorPlacement> Decor { get; } = new List<DecorPlacement>();
        public int SpawnX { get; set; }
        public int FinishX { get; set; }
        /// <summary>Left column of the crew's caravan, parked just past the finish line as at
        /// the end of the Tutorial; street dressing keeps off its <see cref="CaravanWidth"/> columns.</summary>
        public int CaravanX { get; set; }
        /// <summary>How many times the grammar re-rolled to pass the traversal check.</summary>
        public int Attempts { get; set; } = 1;

        public int Width => Ground.Count;

        public int GroundAt(int x) => x >= 0 && x < Ground.Count ? Ground[x] : NoGround;

        /// <summary>Par time: the spec's, or <see cref="ParShare"/> of how long the camera's auto-scroll
        /// takes from spawn to finish. Riding the scroll is far over par; beating it takes running
        /// well ahead of the camera.</summary>
        public float Par => Spec.parTime > 0f
            ? Spec.parTime
            : ParShare * (FinishX - SpawnX) / UnityEngine.Mathf.Max(0.1f, Spec.scrollSpeed);

        /// <summary>A playtest finished at 52% of the full scroll time with 95% wrecked (2026-10-27):
        /// at 55% such a run just makes par.</summary>
        public const float ParShare = 0.55f;

        /// <summary>Lowest ground surface among columns [x0, x1]; pits don't count (they have
        /// no floor). <see cref="NoGround"/> when the range holds no ground.</summary>
        public int LowestGroundIn(int x0, int x1)
        {
            int low = NoGround;
            for (int x = System.Math.Max(0, x0); x <= x1 && x < Ground.Count; x++)
                if (Ground[x] != NoGround && (low == NoGround || Ground[x] < low))
                    low = Ground[x];
            return low;
        }

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
                // Rounded, not cut off: float maths differ by runtime, and -0.4 x 10 can land just above -4.
                foreach (PickupPlacement p in Pickups) { Mix(UnityEngine.Mathf.RoundToInt(p.X * 10f)); Mix(UnityEngine.Mathf.RoundToInt(p.Y * 10f)); Mix((int)p.Kind); Mix(p.Amount); }
                Mix(SpawnX);
                Mix(FinishX);
                return (int)h;
            }
        }
    }
}
