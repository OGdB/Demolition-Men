using System.Collections.Generic;
using Demolition.Core;
using UnityEngine;

namespace Demolition.Generation
{
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

        /// <summary>Where the back wall shows: every open or see-through cell between a row's
        /// outermost blocks. An open cell hangs from the nearest block above it (its ceiling;
        /// below if there is none), a see-through cell from its own block.</summary>
        public List<InteriorCell> Interior()
        {
            var cells = new List<InteriorCell>();
            for (int y = 0; y < Height; y++)
            {
                int min = int.MaxValue, max = int.MinValue;
                for (int x = 0; x < Width; x++)
                    if (!IsFree(x, y))
                    {
                        min = Mathf.Min(min, x);
                        max = Mathf.Max(max, x);
                    }
                for (int x = min + 1; x < max; x++)
                {
                    var cell = new Vector2Int(x, y);
                    SpecBlock block = At(cell);
                    if (block != null && !TileKinds.SeeThrough(block.Kind))
                        continue;
                    Vector2Int host = cell;
                    if (block == null && !TryFind(x, y, 1, out host) && !TryFind(x, y, -1, out host))
                        continue;
                    cells.Add(new InteriorCell { Cell = cell, Host = host, UnderCeiling = y + 1 < Height && !IsFree(x, y + 1) });
                }
            }
            return cells;
        }

        private bool TryFind(int x, int y, int dy, out Vector2Int found)
        {
            for (int yy = y + dy; yy >= 0 && yy < Height; yy += dy)
                if (!IsFree(x, yy))
                {
                    found = new Vector2Int(x, yy);
                    return true;
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
                return (int)h;
            }
        }
    }
}
