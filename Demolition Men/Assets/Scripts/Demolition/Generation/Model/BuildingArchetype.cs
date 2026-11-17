using System;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>The props of an archetype's rooms, by the names in <see cref="Furnishing"/>'s
    /// catalog: what a building is for shows in what stands in it.</summary>
    [Serializable]
    public sealed class FurnishPlan
    {
        /// <summary>Props of every storey.</summary>
        public string[] any = new string[0];
        /// <summary>Props of the street-level room only (the shop, the bar, the lobby).</summary>
        public string[] ground = new string[0];
        /// <summary>Props of the storeys above it only.</summary>
        public string[] upper = new string[0];
        /// <summary>The sign on the roof (<c>sign_club</c>), or none.</summary>
        public string roof = "";
        /// <summary>How full the rooms are: the chance a free stretch of floor gets another prop.</summary>
        public float clutter = 0.6f;
        /// <summary>Share of storeys with the lights on, where a theme lights its buildings.</summary>
        public float lights = 0.75f;

        public bool IsEmpty => Length(any) + Length(ground) + Length(upper) == 0 && string.IsNullOrEmpty(roof);

        private static int Length(string[] list) => list != null ? list.Length : 0;
    }

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
        /// <summary>The street wall of the ground storey when it is plated ("Steel"): no door and no
        /// window in it, the door in the far wall (nobody is locked in), and a window above it on the
        /// first floor: the way in is up (a ladder, a scaffold; <c>LevelGrammar</c> puts them there).
        /// Needs <see cref="columnSpacing"/> and two floors. Empty: a wall like the others.</summary>
        public string shutter = "";

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

        /// <summary>What stands in its rooms and on its roof (<see cref="Furnishing"/>).</summary>
        public FurnishPlan furnish = new FurnishPlan();

        public static BuildingArchetype FromJson(string json) => JsonUtility.FromJson<BuildingArchetype>(json);

        /// <summary>A copy no wider than <paramref name="maxWidth"/> cells (a slot with limited room).</summary>
        public BuildingArchetype WithMaxWidth(int maxWidth)
        {
            var copy = (BuildingArchetype)MemberwiseClone();
            copy.width.max = Mathf.Min(width.max, maxWidth);
            copy.width.min = Mathf.Min(width.min, copy.width.max);
            return copy;
        }

        /// <summary>A copy with one massing rolled once: width, storeys and storey height
        /// pinned, so a row of buildings made from it shares one roofline (a terrace).</summary>
        public BuildingArchetype Pinned(Pcg32 rng)
        {
            var copy = (BuildingArchetype)MemberwiseClone();
            int w = width.Pick(rng), f = floors.Pick(rng), h = floorHeight.Pick(rng);
            copy.width = new IntRange(w, w);
            copy.floors = new IntRange(f, f);
            copy.floorHeight = new IntRange(h, h);
            return copy;
        }

        /// <summary>Rough size, for ordering archetypes from small to big.</summary>
        public int Bulk => width.max * floors.max * floorHeight.max;

        public bool IsTower => id.Contains("tower");

        public bool Plated => !string.IsNullOrEmpty(shutter);

        public static TileKind ParseTile(string name, TileKind fallback) =>
            Enum.TryParse(name, true, out TileKind kind) ? kind : fallback;
    }
}
