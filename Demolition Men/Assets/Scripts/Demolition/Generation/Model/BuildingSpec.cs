using System.Collections.Generic;
using Demolition.Core;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>Which of a building's outer corners a block forms (see <see cref="BuildingSpec.EdgeOf"/>).</summary>
    [System.Flags]
    public enum BlockEdge { None = 0, Left = 1, Right = 2 }

    /// <summary>One block of a generated building: a rectangle of cells.</summary>
    public sealed class SpecBlock
    {
        public TileKind Kind;
        public RectInt Rect;
        public bool Anchor;
        /// <summary>Picks among a tileset's sprite variants.</summary>
        public int Variant;

        public BlockMaterial Material => TileKinds.Material(Kind);

        public IEnumerable<Vector2Int> Cells()
        {
            for (int y = Rect.yMin; y < Rect.yMax; y++)
                for (int x = Rect.xMin; x < Rect.xMax; x++)
                    yield return new Vector2Int(x, y);
        }
    }

    /// <summary>A ladder: a column players climb by holding up, from <see cref="Y"/> until their
    /// feet are at <see cref="Top"/>. No block: it can't be hit, carries nothing, and goes when
    /// the block cell it is fixed to (<see cref="Host"/>) leaves the structure.</summary>
    public sealed class SpecLadder
    {
        /// <summary>Its column; -1 is the street in front of the left wall.</summary>
        public int X;
        public int Y, Top;
        public Vector2Int Host;
    }

    /// <summary>
    /// A generated building as data: blocks on a cell grid with (0,0) at the bottom-left,
    /// ground row y = 0. Turned into GameObjects by the builder; judged by the validator.
    /// </summary>
    /// <summary>One cell of a building's back wall (see <see cref="BuildingSpec.Interior"/>).</summary>
    public struct InteriorCell
    {
        public Vector2Int Cell;
        /// <summary>The block cell it hangs from: when that cell goes, this piece of wall goes.</summary>
        public Vector2Int Host;
        /// <summary>Right under a ceiling (draws the ceiling's shadow).</summary>
        public bool UnderCeiling;
        /// <summary>Storey the cell is on (0 = street level): each storey gets one back-wall look.</summary>
        public int Storey;
        /// <summary>Behind a see-through block (a support, a slab, a window) rather than an open room.</summary>
        public bool SeeThrough;
    }

    public sealed class BuildingSpec
    {
        private readonly Dictionary<Vector2Int, SpecBlock> _occupied = new Dictionary<Vector2Int, SpecBlock>();

        public BuildingSpec(string archetype, int seed)
        {
            Archetype = archetype;
            Seed = seed;
        }

        public string Archetype { get; }
        public int Seed { get; }
        public List<SpecBlock> Blocks { get; } = new List<SpecBlock>();
        /// <summary>What stands in the rooms and on the roof (<see cref="Furnishing"/>): looks only,
        /// so not in <see cref="Hash"/>.</summary>
        public List<SpecProp> Props { get; } = new List<SpecProp>();
        /// <summary>Up the stairwells, and maybe up the street wall (<see cref="AddStreetLadder"/>).</summary>
        public List<SpecLadder> Ladders { get; } = new List<SpecLadder>();
        /// <summary>Storeys with the lights on, where the theme lights its buildings.</summary>
        public HashSet<int> LitStoreys { get; } = new HashSet<int>();
        public string Name => $"{Archetype} #{Seed}";

        public int CellCount => _occupied.Count;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public SpecBlock At(Vector2Int cell) => _occupied.TryGetValue(cell, out SpecBlock block) ? block : null;
        public bool IsFree(int x, int y) => !_occupied.ContainsKey(new Vector2Int(x, y));

        /// <summary>Place a block with its bottom-left cell at (x, y); false if any cell is taken.</summary>
        public bool TryPlace(TileKind kind, int x, int y, int variant = 0)
        {
            Vector2Int size = TileKinds.Size(kind);
            for (int dy = 0; dy < size.y; dy++)
                for (int dx = 0; dx < size.x; dx++)
                    if (!IsFree(x + dx, y + dy))
                        return false;

            var block = new SpecBlock { Kind = kind, Rect = new RectInt(x, y, size.x, size.y), Anchor = y == 0, Variant = variant };
            Blocks.Add(block);
            foreach (Vector2Int cell in block.Cells())
                _occupied[cell] = block;
            Width = Mathf.Max(Width, block.Rect.xMax);
            Height = Mathf.Max(Height, block.Rect.yMax);
            return true;
        }

        /// <summary>The outermost occupied columns of row y, or false for an empty row.</summary>
        private bool RowExtent(int y, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;
            for (int x = 0; x < Width; x++)
                if (!IsFree(x, y))
                {
                    min = Mathf.Min(min, x);
                    max = Mathf.Max(max, x);
                }
            return max >= min;
        }

        /// <summary>The building's silhouette edges a block forms: Left when it is the
        /// outermost block of every row it spans, Right likewise. The tileset draws corner
        /// trim (quoins, posts, pilasters) on those; a one-column building is both.</summary>
        public BlockEdge EdgeOf(SpecBlock block)
        {
            BlockEdge edge = BlockEdge.Left | BlockEdge.Right;
            for (int y = block.Rect.yMin; y < block.Rect.yMax && edge != BlockEdge.None; y++)
            {
                RowExtent(y, out int min, out int max);
                if (block.Rect.xMin != min)
                    edge &= ~BlockEdge.Left;
                if (block.Rect.xMax - 1 != max)
                    edge &= ~BlockEdge.Right;
            }
            return edge;
        }

        private static bool IsFloor(TileKind kind) =>
            kind == TileKind.Plank || kind == TileKind.Beam || kind == TileKind.MetalTruss;

        /// <summary>Where the back wall shows: every open or see-through cell between a row's
        /// outermost blocks, and every window, outer walls included. An open cell or a window
        /// hangs from the nearest solid block above it (its ceiling, the lintel; below if there
        /// is none), so a broken window still shows the room; a see-through cell hangs from its
        /// own block. Storeys count up from the street at every row holding a floor slab.</summary>
        public List<InteriorCell> Interior()
        {
            var cells = new List<InteriorCell>();
            HashSet<int> slabRows = SlabRows();
            int storey = 0;
            for (int y = 0; y < Height; y++)
            {
                if (y > 0 && slabRows.Contains(y))
                    storey++;
                if (!RowExtent(y, out int min, out int max))
                    continue;
                for (int x = min; x <= max; x++)
                {
                    var cell = new Vector2Int(x, y);
                    SpecBlock block = At(cell);
                    bool window = block != null && block.Kind == TileKind.Glass;
                    if (!window && (x == min || x == max || (block != null && !TileKinds.SeeThrough(block.Kind))))
                        continue;
                    Vector2Int host = cell;
                    if ((block == null || window) && !TryFind(x, y, 1, out host) && !TryFind(x, y, -1, out host))
                        continue;
                    SpecBlock above = At(new Vector2Int(x, y + 1));
                    cells.Add(new InteriorCell
                    {
                        Cell = cell, Host = host, Storey = storey, SeeThrough = block != null,
                        UnderCeiling = above != null && above != block, // a window's lower half isn't
                    });
                }
            }
            return cells;
        }

        /// <summary>Rows a floor slab (or a roof of floor boards) runs through.</summary>
        public HashSet<int> SlabRows()
        {
            var rows = new HashSet<int>();
            foreach (SpecBlock b in Blocks)
                if (IsFloor(b.Kind))
                    rows.Add(b.Rect.yMin);
            return rows;
        }

        /// <summary>Storey of row y (0 = street level), counted as <see cref="Interior"/> counts.</summary>
        public int StoreyOf(int y)
        {
            int storey = 0;
            foreach (int row in SlabRows())
                if (row > 0 && row <= y)
                    storey++;
            return storey;
        }

        /// <summary>Its street wall is plated at ground level (an archetype's <c>shutter</c>): the
        /// way in is up.</summary>
        public bool Plated => At(Vector2Int.zero)?.Kind == TileKind.Steel;

        /// <summary>How high the first way in above the street is, on the side players come from:
        /// the sill of the left wall's first-floor window, or the roof of a building of one storey;
        /// -1 when there is neither within <see cref="HighestWayIn"/> rows.</summary>
        public int WayIn()
        {
            for (int y = 3; y <= HighestWayIn; y++)
            {
                SpecBlock block = At(new Vector2Int(0, y));
                if (block == null)
                    return y > 3 ? y : -1; // above the roof's edge (a wall is at least 4 rows)
                if (block.Kind == TileKind.Glass)
                    return y;
            }
            return -1;
        }

        /// <summary>A storey is at most 6 rows, so a window sill or a low roof is at most this high.</summary>
        public const int HighestWayIn = 7;

        /// <summary>A ladder in the street against the left wall, up to <see cref="WayIn"/>; false
        /// when there is nothing to climb to.</summary>
        public bool AddStreetLadder()
        {
            int top = WayIn();
            if (top < 0)
                return false;
            Ladders.Add(new SpecLadder { X = -1, Y = 0, Top = top, Host = new Vector2Int(0, top - 1) });
            return true;
        }

        /// <summary>Whether a prop that gives light is on: the ceiling lights are on their room's
        /// switch; a screen, a fridge, the roof sign always are.</summary>
        public bool Glows(SpecProp prop) =>
            !(Furnishing.TryGet(prop.Name, out PropInfo info) && info.Slot == PropSlot.Ceiling) || LitStoreys.Contains(prop.Storey);

        /// <summary>Whether a block that gives light is on: a window glows when its room's lights
        /// are on; a door's pane, a sign box on the roof always do.</summary>
        public bool Glows(SpecBlock block) => block.Kind != TileKind.Glass || LitStoreys.Contains(StoreyOf(block.Rect.yMin));

        /// <summary>The nearest block cell from (x, y) along dy that isn't glass: a pane breaks
        /// long before the wall around it, so it holds no back wall.</summary>
        public bool TryFind(int x, int y, int dy, out Vector2Int found)
        {
            for (int yy = y + dy; yy >= 0 && yy < Height; yy += dy)
            {
                SpecBlock block = At(new Vector2Int(x, yy));
                if (block != null && block.Kind != TileKind.Glass)
                {
                    found = new Vector2Int(x, yy);
                    return true;
                }
            }
            found = default;
            return false;
        }

        /// <summary>The structural model the game will run on this building.</summary>
        public BuildingStructure ToStructure()
        {
            var structure = new BuildingStructure();
            foreach (SpecBlock block in Blocks)
                foreach (Vector2Int cell in block.Cells())
                    structure.AddCell(cell, block.Anchor, block.Material == BlockMaterial.Glass);
            return structure;
        }

        /// <summary>Order-sensitive content hash (FNV-1a): equal specs hash equal.</summary>
        public int Hash()
        {
            unchecked
            {
                uint h = 2166136261;
                void Mix(int v) { h = (h ^ (uint)v) * 16777619; }
                foreach (SpecBlock b in Blocks)
                {
                    Mix((int)b.Kind);
                    Mix(b.Rect.x);
                    Mix(b.Rect.y);
                    Mix(b.Anchor ? 1 : 0);
                    Mix(b.Variant);
                }
                foreach (SpecLadder ladder in Ladders) // a rule, unlike the props: where a player may climb
                {
                    Mix(ladder.X);
                    Mix(ladder.Y);
                    Mix(ladder.Top);
                }
                return (int)h;
            }
        }
    }
}
