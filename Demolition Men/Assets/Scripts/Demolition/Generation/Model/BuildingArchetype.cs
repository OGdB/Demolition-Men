using System;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// Rules for one kind of building, read from JSON (<c>Assets/Content/&lt;theme&gt;/Archetypes</c>)
    /// so people and agents can write them without touching code. Tile names are
    /// <see cref="TileKind"/> values.
    /// </summary>
    [Serializable]
    public sealed class BuildingArchetype
    {
        public string id = "unnamed";

        /// <summary>Total width in cells, walls included.</summary>
        public IntRange width = new IntRange(7, 11);
        public IntRange floors = new IntRange(2, 3);
        /// <summary>Rows per storey, its floor slab included (at least 4: slab + room for a player).</summary>
        public IntRange floorHeight = new IntRange(4, 4);
        /// <summary>Storey (1-based) from which the building narrows; 0 = never.</summary>
        public IntRange setbackFloor = new IntRange(0, 0);
        /// <summary>Cells trimmed from each side at the setback.</summary>
        public IntRange setback = new IntRange(1, 2);

        public string wall = nameof(TileKind.Brick);
        /// <summary>Brick walls: chance a two-row brick replaces two single bricks.</summary>
        public float tallBrickChance = 0.3f;
        public string floor = nameof(TileKind.Plank);
        public string roof = nameof(TileKind.Beam);

        /// <summary>Interior support spacing in cells; 0 = no interior supports.</summary>
        public IntRange columnSpacing = new IntRange(0, 0);
        /// <summary>Chance per wall per storey of a window.</summary>
        public float windowChance = 0.8f;
        public bool door = true;
        public float chimneyChance;

        /// <summary>How many ground-level blocks may each bring down
        /// <see cref="weakPointCascade"/> of the building.</summary>
        public IntRange weakPoints = new IntRange(1, 3);
        public float weakPointCascade = 0.3f;

        public static BuildingArchetype FromJson(string json) => JsonUtility.FromJson<BuildingArchetype>(json);

        /// <summary>A copy no wider than <paramref name="maxWidth"/> cells (a slot with limited room).</summary>
        public BuildingArchetype WithMaxWidth(int maxWidth)
        {
            var copy = (BuildingArchetype)MemberwiseClone();
            copy.width.max = Mathf.Min(width.max, maxWidth);
            copy.width.min = Mathf.Min(width.min, copy.width.max);
            return copy;
        }

        /// <summary>Rough size, for ordering archetypes from small to big.</summary>
        public int Bulk => width.max * floors.max * floorHeight.max;

        public bool IsTower => id.Contains("tower");

        public static TileKind ParseTile(string name, TileKind fallback) =>
            Enum.TryParse(name, true, out TileKind kind) ? kind : fallback;
    }
}
