using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// Archetype + seed → <see cref="BuildingSpec"/>. A building is a stack of storeys:
    /// two walls (windows, a door at street level), a floor slab between storeys with a
    /// stairwell gap players can jump through, optional interior supports, optional
    /// setback (upper storeys narrower, resting on the slab), then a roof and maybe a
    /// chimney. Everything on the ground row is an anchor.
    ///
    /// Glass carries nothing, so a wall with a window hangs from the slab above it. The
    /// supports are the backbone: stairwells never cut a slab piece off from them, and
    /// buildings without supports only get street-level windows.
    /// </summary>
    public static class BuildingGrammar
    {
        public const int MinStorey = 4;
        public const int MaxAttempts = 24;

        /// <summary>Generate, then validate; retry with derived seeds until the building
        /// stands and hits the archetype's weak-point target. Same seed, same result.</summary>
        public static BuildingSpec GenerateValid(BuildingArchetype archetype, int seed, out BuildingReport report)
        {
            BuildingSpec best = null;
            report = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int attemptSeed = attempt == 0 ? seed : unchecked(seed * 31 + attempt * 7919);
                BuildingSpec spec = Generate(archetype, attemptSeed);
                BuildingReport r = BuildingValidator.Validate(spec, archetype);
                r.Attempts = attempt + 1;
                if (r.MeetsTargets)
                {
                    report = r;
                    return spec;
                }
                if (best == null || (r.Supported && !report.Supported))
                {
                    best = spec;
                    report = r;
                }
            }
            return best;
        }

        public static BuildingSpec Generate(BuildingArchetype a, int seed)
        {
            var rng = new Pcg32(seed);
            var spec = new BuildingSpec(a.id, seed);

            int width = Mathf.Max(5, a.width.Pick(rng));
            int floors = Mathf.Max(1, a.floors.Pick(rng));
            int storey = Mathf.Max(MinStorey, a.floorHeight.Pick(rng));
            int fullStoreys = a.setbackFloor.max > 0 ? Mathf.Max(1, a.setbackFloor.Pick(rng)) : int.MaxValue;
            int setback = Mathf.Max(0, a.setback.Pick(rng));
            int spacing = Mathf.Max(0, a.columnSpacing.Pick(rng));
            TileKind wall = BuildingArchetype.ParseTile(a.wall, TileKind.Brick);
            TileKind floor = BuildingArchetype.ParseTile(a.floor, TileKind.Plank);
            TileKind roof = BuildingArchetype.ParseTile(a.roof, TileKind.Beam);
            bool doorLeft = rng.Chance(0.5f);

            int prevLeft = 0, prevRight = width - 1;
            List<int> prevSupports = SupportXs(spacing, prevLeft, prevRight);
            for (int s = 0; s < floors; s++)
            {
                int inset = s >= fullStoreys ? setback : 0;
                int left = inset, right = width - 1 - inset;
                if (right - left < 4)
                {
                    left = prevLeft; // too narrow to set back: keep the width below
                    right = prevRight;
                }

                int y0 = s * storey;
                List<int> supports = SupportXs(spacing, left, right);
                if (s > 0)
                {
                    int from = Mathf.Min(left, prevLeft), to = Mathf.Max(right, prevRight);
                    int gap = Stairwell(prevSupports, supports, from, to, left, right, s % 2 == 1);
                    Slab(spec, floor, y0, from, to, left, right, gap);
                }
                bool door = s == 0 && a.door;
                float windows = s == 0 || spacing > 0 ? a.windowChance : 0f;
                Wall(spec, rng, wall, left, y0, storey, door && doorLeft, windows, a.tallBrickChance);
                Wall(spec, rng, wall, right, y0, storey, door && !doorLeft, windows, a.tallBrickChance);
                foreach (int x in supports)
                    for (int y = s == 0 ? 0 : y0 + 1; y < y0 + storey; y++)
                        spec.TryPlace(TileKind.Support, x, y);
                prevLeft = left;
                prevRight = right;
                prevSupports = supports;
            }

            int roofY = floors * storey;
            Roof(spec, roof, prevLeft, prevRight, roofY);
            if (rng.Chance(a.chimneyChance))
            {
                int x = rng.Range(prevLeft + 1, prevRight - 1);
                int height = rng.Range(1, 2);
                for (int i = 0; i < height; i++)
                    spec.TryPlace(TileKind.Chimney, x, roofY + 1 + i);
            }
            return spec;
        }

        private static void Wall(BuildingSpec spec, Pcg32 rng, TileKind kind, int x, int y0, int storey, bool door,
            float windowChance, float tallBrickChance)
        {
            int top = y0 + storey - 1;
            if (door)
                spec.TryPlace(TileKind.Door, x, y0);
            else if (rng.Chance(windowChance))
                spec.TryPlace(TileKind.Glass, x, y0 + 1);

            for (int y = y0; y <= top; y++)
            {
                if (!spec.IsFree(x, y))
                    continue;
                int variant = (int)(rng.Next() & 0xFF);
                bool tall = kind == TileKind.Brick && y < top && spec.IsFree(x, y + 1) && rng.Chance(tallBrickChance);
                spec.TryPlace(tall ? TileKind.BrickTall : kind, x, y, variant);
            }
        }

        private static List<int> SupportXs(int spacing, int left, int right)
        {
            var xs = new List<int>();
            if (spacing > 0)
                for (int x = left + spacing; x <= right - 2; x += spacing)
                    xs.Add(x);
            return xs;
        }

        // Left cell of a two-cell stairwell, or int.MinValue for none. It keeps clear of
        // the walls and of supports above and below, and both slab pieces it leaves must
        // rest on a support from the storey below. Prefers alternating sides.
        private static int Stairwell(List<int> below, List<int> above, int from, int to, int wallLeft, int wallRight, bool preferLeft)
        {
            int best = int.MinValue;
            for (int g = wallLeft + 2; g + 1 <= wallRight - 2; g++)
            {
                if (below.Contains(g) || below.Contains(g + 1) || above.Contains(g) || above.Contains(g + 1))
                    continue;
                bool leftHeld = false, rightHeld = false;
                foreach (int x in below)
                {
                    leftHeld |= x >= from && x < g;
                    rightHeld |= x > g + 1 && x <= to;
                }
                if (!leftHeld || !rightHeld)
                    continue;
                if (best == int.MinValue || !preferLeft)
                    best = g;
                if (preferLeft)
                    break;
            }
            return best;
        }

        // The slab covers both this storey and the one below (a setback leaves ledges),
        // minus this storey's walls and the stairwell.
        private static void Slab(BuildingSpec spec, TileKind kind, int y, int from, int to, int wallLeft, int wallRight, int gap)
        {
            bool Open(int x) => x >= from && x <= to && x != wallLeft && x != wallRight
                                && x != gap && x != gap + 1 && spec.IsFree(x, y);

            for (int x = from; x <= to; x++)
            {
                if (!Open(x))
                    continue;
                if (TileKinds.Size(kind).x == 2)
                {
                    if (Open(x + 1))
                    {
                        spec.TryPlace(kind, x, y);
                        x++;
                    }
                    else
                    {
                        spec.TryPlace(TileKind.Plank, x, y);
                    }
                }
                else
                {
                    spec.TryPlace(kind, x, y);
                }
            }
        }

        private static void Roof(BuildingSpec spec, TileKind kind, int left, int right, int y)
        {
            TileKind filler = kind == TileKind.Beam ? TileKind.Wood
                : kind == TileKind.BrickCap ? TileKind.Brick
                : kind;
            for (int x = left; x <= right; x++)
            {
                if (TileKinds.Size(kind).x == 2 && x + 1 <= right)
                {
                    spec.TryPlace(kind, x, y);
                    x++;
                }
                else
                {
                    spec.TryPlace(filler, x, y);
                }
            }
        }
    }
}
