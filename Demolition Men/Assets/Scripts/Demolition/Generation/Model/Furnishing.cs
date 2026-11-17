using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>Where a prop belongs in a room.</summary>
    public enum PropSlot
    {
        /// <summary>Stands on the floor (a bar, a bed, a fridge).</summary>
        Floor,
        /// <summary>Hangs on the back wall at eye level (a poster, a neon tube).</summary>
        Wall,
        /// <summary>Hangs from the ceiling (a lamp, a disco ball).</summary>
        Ceiling,
        /// <summary>Stands on the roof (the building's sign).</summary>
        Roof,
    }

    public readonly struct PropInfo
    {
        public readonly PropSlot Slot;
        /// <summary>Size in cells; its sprite is <c>prop_&lt;name&gt;.png</c>, 32 px per cell.</summary>
        public readonly int Width, Height;

        public PropInfo(PropSlot slot, int width, int height)
        {
            Slot = slot;
            Width = width;
            Height = height;
        }
    }

    /// <summary>One prop of a furnished building (cosmetic: no collider, no rule).</summary>
    public sealed class SpecProp
    {
        public string Name;
        public RectInt Rect;
        /// <summary>The block cell it goes with: the floor under it, else what it hangs from.</summary>
        public Vector2Int Host;
        /// <summary>Storey it is on (0 = street level); the roof sign counts as the top storey.</summary>
        public int Storey;
        /// <summary>How far above its cells it is drawn: a lamp under a plank floor hangs from the
        /// plank, which only fills the top half of its cell.</summary>
        public float Lift;
    }

    /// <summary>
    /// What makes a club a club: every archetype names the props of its rooms
    /// (<see cref="FurnishPlan"/>) and this puts them where they belong, each on its
    /// <see cref="PropSlot"/>: furniture along the floor, lamps in a rhythm under the ceiling,
    /// a few things on the wall, the sign on the roof. Every floor prop a storey may have stands
    /// in it once (room allowing) before any stands twice. Cosmetic, and from its own random stream: furnishing
    /// never changes a block.
    /// </summary>
    public static class Furnishing
    {
        /// <summary>Roof signs: any name with this prefix (<c>sign_club</c>) is one.</summary>
        public const string SignPrefix = "sign_";
        /// <summary>How often one prop may stand in one storey.</summary>
        public const int MaxPerStorey = 2;

        private static readonly Dictionary<string, PropInfo> Catalog = new Dictionary<string, PropInfo>
        {
            { "bar", Floor(2, 1) }, { "speaker", Floor(1, 2) }, { "djdesk", Floor(2, 1) },
            { "counter", Floor(2, 1) }, { "fridge", Floor(1, 2) }, { "rack", Floor(1, 2) },
            { "cabinet", Floor(1, 2) }, { "slot", Floor(1, 2) }, { "desk", Floor(2, 1) },
            { "sofa", Floor(2, 1) }, { "bed", Floor(2, 1) }, { "table", Floor(2, 1) },
            { "plant", Floor(1, 1) }, { "filing", Floor(1, 2) }, { "workbench", Floor(2, 1) },
            { "pallet", Floor(2, 1) }, { "racking", Floor(1, 2) }, { "machine", Floor(2, 2) },
            { "tank", Floor(1, 2) }, { "vault", Floor(2, 2) },
            { "poster", Wall(1) }, { "picture", Wall(1) }, { "clock", Wall(1) }, { "shelf", Wall(1) },
            { "pipe", Wall(1) }, { "board", Wall(1) }, { "tv", Wall(1) }, { "neon", Wall(2) }, { "bottles", Wall(2) },
            { "lamp", Ceiling(1) }, { "tube", Ceiling(2) }, { "discoball", Ceiling(1) }, { "spot", Ceiling(1) },
        };

        private static int Width(string name) => TryGet(name, out PropInfo info) ? info.Width : 0;
        private static PropInfo Floor(int w, int h) => new PropInfo(PropSlot.Floor, w, h);
        private static PropInfo Wall(int w) => new PropInfo(PropSlot.Wall, w, 1);
        private static PropInfo Ceiling(int w) => new PropInfo(PropSlot.Ceiling, w, 1);

        /// <summary>Every prop with a fixed name (roof signs are any <see cref="SignPrefix"/> name).</summary>
        public static IEnumerable<string> Names => Catalog.Keys;

        public static bool TryGet(string name, out PropInfo info)
        {
            if (name != null && name.StartsWith(SignPrefix) && name.Length > SignPrefix.Length)
            {
                info = new PropInfo(PropSlot.Roof, 2, 1);
                return true;
            }
            info = default;
            return name != null && Catalog.TryGetValue(name, out info);
        }

        public static void Furnish(BuildingSpec spec, FurnishPlan plan)
        {
            if (plan == null || plan.IsEmpty)
                return;
            var rng = new Pcg32(unchecked(spec.Seed * 7919 + 0xF00D));

            // The rooms: open cells per storey, without the rows a floor slab runs through
            // (a stairwell is no place for a poster) and the ladders' columns.
            HashSet<int> slabRows = spec.SlabRows();
            var rooms = new SortedDictionary<int, HashSet<Vector2Int>>();
            foreach (InteriorCell cell in spec.Interior())
            {
                if (cell.SeeThrough || slabRows.Contains(cell.Cell.y)
                    || spec.Ladders.Exists(l => l.X == cell.Cell.x && cell.Cell.y >= l.Y && cell.Cell.y < l.Top))
                    continue; // nothing stands where the ladder comes up
                if (!rooms.TryGetValue(cell.Storey, out HashSet<Vector2Int> room))
                    rooms[cell.Storey] = room = new HashSet<Vector2Int>();
                room.Add(cell.Cell);
            }

            int top = 0;
            foreach (KeyValuePair<int, HashSet<Vector2Int>> room in rooms)
            {
                top = room.Key;
                if (rng.Chance(plan.lights))
                    spec.LitStoreys.Add(room.Key);
                FurnishRoom(spec, rng, room.Key, room.Value, plan);
            }
            if (TryGet(plan.roof, out PropInfo sign) && sign.Slot == PropSlot.Roof)
                PlaceSign(spec, plan.roof, sign, top);
        }

        private static void FurnishRoom(BuildingSpec spec, Pcg32 rng, int storey, HashSet<Vector2Int> room, FurnishPlan plan)
        {
            int floorY = int.MaxValue, ceilingY = int.MinValue, left = int.MaxValue, right = int.MinValue;
            foreach (Vector2Int cell in room)
            {
                floorY = Mathf.Min(floorY, cell.y);
                ceilingY = Mathf.Max(ceilingY, cell.y);
                left = Mathf.Min(left, cell.x);
                right = Mathf.Max(right, cell.x);
            }
            var names = new Dictionary<PropSlot, List<string>>();
            foreach (string[] list in new[] { plan.any, storey == 0 ? plan.ground : plan.upper })
                foreach (string name in list ?? new string[0])
                    if (TryGet(name, out PropInfo info) && info.Slot != PropSlot.Roof)
                    {
                        if (!names.TryGetValue(info.Slot, out List<string> slot))
                            names[info.Slot] = slot = new List<string>();
                        if (!slot.Contains(name))
                            slot.Add(name);
                    }
            var free = new HashSet<Vector2Int>(room);
            var count = new Dictionary<string, int>();

            bool Fits(PropInfo info, int x, int y)
            {
                for (int dx = 0; dx < info.Width; dx++)
                {
                    for (int dy = 0; dy < info.Height; dy++)
                        if (!free.Contains(new Vector2Int(x + dx, y + dy)))
                            return false;
                    // A floor prop stands on something, a ceiling prop hangs from something, and
                    // nothing hangs on the wall right over a piece of furniture.
                    if (info.Slot == PropSlot.Floor && y > 0 && spec.IsFree(x + dx, y - 1))
                        return false;
                    if (info.Slot == PropSlot.Ceiling && spec.IsFree(x + dx, y + 1))
                        return false;
                    if (info.Slot == PropSlot.Wall && room.Contains(new Vector2Int(x + dx, y - 1)) && !free.Contains(new Vector2Int(x + dx, y - 1)))
                        return false;
                }
                return true;
            }

            bool Place(string name, int x, int y)
            {
                TryGet(name, out PropInfo info);
                count.TryGetValue(name, out int had);
                if (had >= MaxPerStorey || !Fits(info, x, y))
                    return false;
                var rect = new RectInt(x, y, info.Width, info.Height);
                Vector2Int host;
                if (info.Slot == PropSlot.Floor && y > 0)
                    host = new Vector2Int(x, y - 1);
                else if (!spec.TryFind(x, rect.yMax - 1, 1, out host) && !spec.TryFind(x, y, -1, out host))
                    return false;
                float lift = info.Slot == PropSlot.Ceiling ? TileKinds.Collider(spec.At(new Vector2Int(x, y + 1)).Kind).yMin : 0f;
                spec.Props.Add(new SpecProp { Name = name, Rect = rect, Host = host, Storey = storey, Lift = lift });
                for (int dx = 0; dx < info.Width; dx++)
                    for (int dy = 0; dy < info.Height; dy++)
                        free.Remove(new Vector2Int(x + dx, y + dy));
                count[name] = had + 1;
                return true;
            }

            // Floor: each prop once, the widest first and each against a wall, a column or another
            // prop, so a narrow room still holds them all; then the gaps, as cluttered as the plan says.
            if (names.TryGetValue(PropSlot.Floor, out List<string> floor))
            {
                var widestFirst = new List<string>(floor);
                widestFirst.Sort((p, q) => Width(q).CompareTo(Width(p)) != 0 ? Width(q).CompareTo(Width(p)) : floor.IndexOf(p).CompareTo(floor.IndexOf(q)));
                foreach (string name in widestFirst)
                {
                    TryGet(name, out PropInfo info);
                    var fits = new List<int>();
                    for (int x = left; x <= right; x++)
                        if (Fits(info, x, floorY)
                            && (!free.Contains(new Vector2Int(x - 1, floorY)) || !free.Contains(new Vector2Int(x + info.Width, floorY))))
                            fits.Add(x);
                    if (fits.Count > 0)
                        Place(name, fits[rng.Range(0, fits.Count - 1)], floorY);
                }
                for (int x = left; x <= right; x++)
                {
                    string name = floor[rng.Range(0, floor.Count - 1)];
                    if (rng.Chance(plan.clutter) && Place(name, x, floorY))
                        x += spec.Props[spec.Props.Count - 1].Rect.width; // and a gap of one
                }
            }

            // Ceiling: lamps hang in a rhythm, the plan's kinds in turn.
            if (ceilingY > floorY && names.TryGetValue(PropSlot.Ceiling, out List<string> ceiling))
            {
                int turn = rng.Range(0, ceiling.Count - 1);
                for (int x = left + rng.Range(0, 1); x <= right; x++)
                    if (Place(ceiling[turn % ceiling.Count], x, ceilingY))
                    {
                        turn++;
                        x += spec.Props[spec.Props.Count - 1].Rect.width + rng.Range(1, 2);
                    }
            }

            // Wall: a few things at eye level (a row over the furniture where the room is high
            // enough), where no tall furniture stands.
            int eyeY = ceilingY - floorY >= 3 ? floorY + 2 : floorY + 1;
            if (eyeY > floorY && eyeY < ceilingY && names.TryGetValue(PropSlot.Wall, out List<string> wall))
                for (int x = left; x <= right; x++)
                {
                    string name = wall[rng.Range(0, wall.Count - 1)];
                    if (rng.Chance(plan.clutter * 0.6f) && Place(name, x, eyeY))
                        x += spec.Props[spec.Props.Count - 1].Rect.width;
                }
        }

        // On the roof, as near its middle as the chimney leaves room.
        private static void PlaceSign(BuildingSpec spec, string name, PropInfo info, int storey)
        {
            int roofY = 0;
            foreach (SpecBlock b in spec.Blocks)
                if (b.Kind != TileKind.Chimney)
                    roofY = Mathf.Max(roofY, b.Rect.yMax);
            int best = int.MinValue;
            float middle = (spec.Width - info.Width) * 0.5f;
            for (int x = 0; x + info.Width <= spec.Width; x++)
            {
                bool fits = true;
                for (int dx = 0; dx < info.Width; dx++)
                    fits &= spec.IsFree(x + dx, roofY) && !spec.IsFree(x + dx, roofY - 1);
                if (fits && (best == int.MinValue || Mathf.Abs(x - middle) < Mathf.Abs(best - middle)))
                    best = x;
            }
            if (best != int.MinValue)
                spec.Props.Add(new SpecProp
                {
                    Name = name, Rect = new RectInt(best, roofY, info.Width, info.Height),
                    Host = new Vector2Int(best, roofY - 1), Storey = storey,
                });
        }
    }
}
