using System;
using System.Collections.Generic;
using Demolition.Generation;
using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>
    /// Spec + seed → <see cref="LevelLayout"/> (roadmap §5.2). Walks the pacing tokens left
    /// to right, writing ground columns, building placements (each a validated
    /// <see cref="BuildingSpec"/>) and pickup spots, then spends the pickup budget. A layout
    /// the <see cref="TraversalValidator"/> rejects is re-rolled from a derived seed.
    /// Uses <see cref="Pcg32"/> only, so the same spec gives the same level everywhere.
    /// </summary>
    public static class LevelGrammar
    {
        public const int MaxAttempts = 16;
        /// <summary>Widest pit the grammar digs (the validator still checks it).</summary>
        public const int MaxPit = 3;
        /// <summary>Archetype prefix of street props (see <see cref="BuildingPlacement.IsProp"/>).</summary>
        public const string PropPrefix = "prop.";
        /// <summary>Breakable props a level uses when its spec names none.</summary>
        public static readonly string[] DefaultProps = { "crate", "car", "barrel" };
        /// <summary>Chance that a free stretch of street gets a prop, and then each further one.</summary>
        public const float PropChance = 0.65f;
        /// <summary>Widest stretch of street between two buildings that gets a fence along it.</summary>
        public const int MaxFencedGap = 8;
        /// <summary>Street left of the spawn: the players run in over it during the countdown
        /// (RunController.RunIn), 3 s in the fastest boots (10 m/s) plus player 2's step back.</summary>
        public const int RunUp = 32;

        private struct Spot
        {
            public float X, Y;
            public bool Roof;
            public int Segment;
        }

        public static LevelLayout Generate(LevelSpec spec, IReadOnlyList<BuildingArchetype> archetypes) =>
            Generate(spec, archetypes, JumpProfile.Default, out _);

        public static LevelLayout Generate(LevelSpec spec, IReadOnlyList<BuildingArchetype> archetypes, JumpProfile jump,
            out TraversalReport report)
        {
            LevelLayout layout = null;
            report = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int seed = attempt == 0 ? spec.seed : unchecked(spec.seed * 31 + attempt * 104729);
                layout = Build(spec, seed, Filter(spec, archetypes), jump);
                layout.Attempts = attempt + 1;
                report = TraversalValidator.Validate(layout, jump);
                if (report.Passable)
                    break;
            }
            return layout;
        }

        /// <summary>Allowed archetypes, smallest first.</summary>
        private static List<BuildingArchetype> Filter(LevelSpec spec, IReadOnlyList<BuildingArchetype> archetypes)
        {
            var list = new List<BuildingArchetype>();
            foreach (BuildingArchetype a in archetypes)
                if (spec.archetypes == null || spec.archetypes.Length == 0 || Array.IndexOf(spec.archetypes, a.id) >= 0)
                    list.Add(a);
            if (list.Count == 0)
                list.AddRange(archetypes);
            list.Sort((a, b) => a.Bulk != b.Bulk ? a.Bulk.CompareTo(b.Bulk) : string.CompareOrdinal(a.id, b.id));
            return list;
        }

        private static LevelLayout Build(LevelSpec spec, int seed, List<BuildingArchetype> archetypes, JumpProfile jump)
        {
            var rng = new Pcg32(seed);
            var layout = new LevelLayout(spec);
            var spots = new List<Spot>();
            int height = 0;
            int maxRise = Mathf.Max(1, Mathf.Min(2, TraversalValidator.MaxRise(jump)));

            var tokens = new List<string>(spec.pacing ?? new string[0]);
            if (tokens.Count == 0 || Parse(tokens[0], out _) != SegmentKind.Start)
                tokens.Insert(0, "start");
            if (Parse(tokens[tokens.Count - 1], out _) != SegmentKind.Finish)
                tokens.Add("finish");

            for (int i = 0; i < tokens.Count; i++)
            {
                SegmentKind kind = Parse(tokens[i], out int count);
                var segment = new LevelSegment { Kind = kind, X0 = layout.Width };
                int index = layout.Segments.Count;
                float progress = tokens.Count > 1 ? i / (float)(tokens.Count - 1) : 0f;

                switch (kind)
                {
                    case SegmentKind.Start:
                        Flat(layout, RunUp + 14, height);
                        layout.SpawnX = segment.X0 + RunUp + 4;
                        break;
                    case SegmentKind.Street:
                        Flat(layout, rng.Range(3, 5), height, spots, index);
                        PlaceBuilding(layout, rng, Pick(archetypes, rng, progress, false), height, index, spots);
                        Flat(layout, rng.Range(3, 5), height, spots, index);
                        break;
                    case SegmentKind.Cluster:
                        // A row of different buildings standing wall to wall.
                        Flat(layout, 3, height, spots, index);
                        for (int b = 0; b < Mathf.Max(1, count); b++)
                            PlaceBuilding(layout, rng, Pick(archetypes, rng, progress, false), height, index, spots);
                        Flat(layout, 3, height, spots, index);
                        break;
                    case SegmentKind.Terrace:
                        // A terrace: one archetype at one size, wall to wall, so the roofs make a
                        // continuous run players can take instead of the street.
                        Flat(layout, 3, height, spots, index);
                        BuildingArchetype pinned = Pick(archetypes, rng, progress, false).Pinned(rng);
                        for (int b = 0; b < Mathf.Max(1, count); b++)
                            PlaceBuilding(layout, rng, pinned, height, index, spots);
                        Flat(layout, 3, height, spots, index);
                        break;
                    case SegmentKind.Yard:
                        // A lot full of breakables (crates, cars, gas tanks, containers), one
                        // free column between them, with no building to hide behind.
                        Flat(layout, 2, height, spots, index);
                        for (int b = 0, n = Mathf.Max(1, count); b < n; b++)
                        {
                            List<string> yardKinds = PropKinds(spec);
                            Place(layout, Prop(yardKinds[rng.Range(0, yardKinds.Count - 1)], rng), height, index, spots);
                            Flat(layout, 1, height, spots, index);
                        }
                        Flat(layout, 2, height, spots, index);
                        break;
                    case SegmentKind.Tower:
                        Flat(layout, 4, height, spots, index);
                        PlaceBuilding(layout, rng, Pick(archetypes, rng, 1f, true), height, index, spots);
                        Flat(layout, 4, height, spots, index);
                        break;
                    case SegmentKind.Gap:
                        Flat(layout, 3, height, spots, index);
                        Pit(layout, rng.Range(2, MaxPit));
                        if (rng.Chance(0.4f))
                            height += rng.Chance(0.5f) ? 1 : -1;
                        Flat(layout, 3, height, spots, index);
                        break;
                    case SegmentKind.Hill:
                        height = Hill(layout, rng, height, maxRise, spots, index);
                        break;
                    case SegmentKind.Plateau:
                        height = Plateau(layout, rng, Pick(archetypes, rng, progress, false), height, maxRise, index, spots);
                        break;
                    case SegmentKind.Islands:
                        Flat(layout, 3, height, spots, index);
                        for (int b = 0; b < Mathf.Max(1, count); b++)
                        {
                            PlaceBuilding(layout, rng, Pick(archetypes, rng, progress, false), height, index, spots);
                            // A ledge, a pit and a landing strip: the next building's wall never
                            // stands at the pit's edge, and debris falls out of the street.
                            Flat(layout, 1, height, spots, index);
                            Pit(layout, rng.Range(2, MaxPit));
                            if (rng.Chance(0.3f))
                                height += rng.Chance(0.5f) ? 1 : -1;
                            Flat(layout, 2, height, spots, index);
                        }
                        Flat(layout, 1, height, spots, index);
                        break;
                    case SegmentKind.Sewer:
                        height = Sewer(layout, rng, height, spots, index);
                        break;
                    case SegmentKind.Gate:
                        Flat(layout, 4, height, spots, index);
                        Place(layout, Gate(rng, rng.Range(5, 7)), height, index, spots);
                        Flat(layout, 4, height, spots, index);
                        break;
                    case SegmentKind.Breather:
                        Flat(layout, rng.Range(8, 11), height, spots, index);
                        break;
                    case SegmentKind.Finish:
                        Flat(layout, 6, height);
                        layout.FinishX = layout.Width;
                        layout.CaravanX = layout.FinishX + 1;
                        Flat(layout, 16, height);
                        break;
                }
                segment.X1 = layout.Width;
                layout.Segments.Add(segment);
            }

            Dress(layout, seed, spots);
            SpendPickups(layout, rng, spots);
            return layout;
        }

        public static SegmentKind Parse(string token, out int count)
        {
            count = 1;
            string name = token ?? string.Empty;
            int colon = name.IndexOf(':');
            if (colon >= 0)
            {
                int.TryParse(name.Substring(colon + 1), out count);
                name = name.Substring(0, colon);
            }
            return Enum.TryParse(name.Trim(), true, out SegmentKind kind) ? kind : SegmentKind.Breather;
        }

        // ---------------- Ground ----------------

        private static void Flat(LevelLayout layout, int columns, int height, List<Spot> spots = null, int segment = 0)
        {
            for (int i = 0; i < columns; i++)
            {
                if (spots != null && i % 2 == 1)
                    spots.Add(new Spot { X = layout.Width + 0.5f, Y = height + 0.6f, Segment = segment });
                layout.Ground.Add(height);
            }
        }

        private static void Pit(LevelLayout layout, int columns)
        {
            for (int i = 0; i < columns; i++)
                layout.Ground.Add(LevelLayout.NoGround);
        }

        // Up a few steps, a plateau, and back down: never steeper than a safe jump.
        private static int Hill(LevelLayout layout, Pcg32 rng, int height, int maxRise, List<Spot> spots, int segment)
        {
            int steps = rng.Range(1, 2);
            int top = height;
            Flat(layout, 2, height, spots, segment);
            for (int s = 0; s < steps; s++)
            {
                top += rng.Range(1, maxRise);
                Flat(layout, rng.Range(3, 5), top, spots, segment);
            }
            Flat(layout, rng.Range(4, 8), top, spots, segment);
            int end = height + (rng.Chance(0.3f) ? 1 : 0);
            while (top > end)
            {
                top = Mathf.Max(end, top - rng.Range(1, maxRise));
                Flat(layout, rng.Range(3, 4), top, spots, segment);
            }
            return top;
        }

        // Steps up to a building standing on the high ground, and back down past it.
        private static int Plateau(LevelLayout layout, Pcg32 rng, BuildingArchetype archetype, int height, int maxRise,
            int segment, List<Spot> spots)
        {
            Flat(layout, 2, height, spots, segment);
            int top = height;
            for (int s = 0, steps = rng.Range(1, 2); s < steps; s++)
            {
                top += rng.Range(1, maxRise);
                Flat(layout, 3, top, spots, segment);
            }
            Flat(layout, 2, top, spots, segment);
            PlaceBuilding(layout, rng, archetype, top, segment, spots);
            Flat(layout, 3, top, spots, segment);
            while (top > height)
            {
                top = Mathf.Max(height, top - rng.Range(1, maxRise));
                Flat(layout, 3, top, spots, segment);
            }
            return top;
        }

        // A trench below street level: one-cell steps down, a stretch at the bottom, steps up.
        private static int Sewer(LevelLayout layout, Pcg32 rng, int height, List<Spot> spots, int segment)
        {
            int depth = rng.Range(3, 4);
            Flat(layout, 2, height, spots, segment);
            for (int d = 1; d <= depth; d++)
                Flat(layout, 2, height - d, null, segment);
            Flat(layout, rng.Range(10, 14), height - depth, spots, segment);
            for (int d = depth - 1; d >= 0; d--)
                Flat(layout, 2, height - d, null, segment);
            Flat(layout, 2, height, spots, segment);
            return height;
        }

        // ---------------- Buildings ----------------

        // Smallest archetypes early, the whole range by the end; towers only when asked.
        private static BuildingArchetype Pick(List<BuildingArchetype> archetypes, Pcg32 rng, float progress, bool tower)
        {
            var pool = new List<BuildingArchetype>();
            foreach (BuildingArchetype a in archetypes)
                if (a.IsTower == tower)
                    pool.Add(a);
            if (pool.Count == 0)
            {
                if (tower) // no tower in this theme: the biggest building will do
                    return archetypes[archetypes.Count - 1];
                pool.AddRange(archetypes);
            }
            int reach = Mathf.Clamp(Mathf.CeilToInt(pool.Count * (0.5f + 0.5f * progress)), 1, pool.Count);
            return pool[rng.Range(0, reach - 1)];
        }

        private static void PlaceBuilding(LevelLayout layout, Pcg32 rng, BuildingArchetype archetype, int height, int segment,
            List<Spot> spots)
        {
            int seed = (int)rng.Next();
            Place(layout, BuildingGrammar.GenerateValid(archetype, seed, out _), height, segment, spots);
        }

        private static void Place(LevelLayout layout, BuildingSpec spec, int height, int segment, List<Spot> spots)
        {
            int x = layout.Width;
            var placement = new BuildingPlacement { X = x, GroundY = height, Spec = spec, Segment = segment };
            layout.Buildings.Add(placement);
            if (!placement.IsProp)
                spots.Add(new Spot { X = x + spec.Width * 0.5f, Y = height + spec.Height + 0.6f, Roof = true, Segment = segment });
            Flat(layout, spec.Width, height);
        }

        /// <summary>A wall across the street that has to come down: brick on a weak wooden
        /// footing, two cells thick.</summary>
        public static BuildingSpec Gate(Pcg32 rng, int height)
        {
            var spec = new BuildingSpec("gate", (int)rng.Next());
            for (int x = 0; x < 2; x++)
                spec.TryPlace(TileKind.Wood, x, 0);
            for (int y = 1; y < height; y++)
                for (int x = 0; x < 2; x++)
                    spec.TryPlace(y == height - 1 ? TileKind.BrickCap : TileKind.Brick, x, y, (int)(rng.Next() & 0xFF));
            return spec;
        }

        // ---------------- Street dressing ----------------

        // Breakable props and cosmetic decor in the gaps the course leaves. Their own random
        // stream: dressing never changes the course the traversal check passed.
        private static void Dress(LevelLayout layout, int seed, List<Spot> spots)
        {
            var rng = new Pcg32(unchecked(seed * 7919 + 0x5EED));
            var solid = new bool[layout.Width];   // footprints: no decor here
            var taken = new bool[layout.Width];   // footprints plus their margins: no prop here
            foreach (BuildingPlacement b in layout.Buildings)
            {
                Mark(solid, taken, b.X, b.Spec.Width);
                for (int x = Mathf.Max(0, b.X - 2); x < b.X; x++)
                    taken[x] = true; // its doorstep: nothing blocks the wall, and the weapon spot stays
            }
            Mark(null, taken, layout.SpawnX - 3, 10);   // room to start
            Mark(null, taken, layout.FinishX - 2, 5);   // the finish line stays clear
            Mark(solid, taken, layout.CaravanX, LevelLayout.CaravanWidth);   // the caravan's spot

            // Each free stretch of flat street: props with a free column either side.
            List<string> kinds = PropKinds(layout.Spec);
            for (int x = layout.SpawnX; kinds.Count > 0 && x < layout.FinishX;)
            {
                int ground = layout.GroundAt(x);
                int end = x;
                while (end < layout.FinishX && !taken[end] && ground != LevelLayout.NoGround && layout.GroundAt(end) == ground)
                    end++;
                for (int cursor = x; end - cursor >= 3 && rng.Chance(PropChance);)
                {
                    BuildingSpec prop = Prop(kinds[rng.Range(0, kinds.Count - 1)], rng);
                    int room = end - cursor - prop.Width - 2;
                    if (room < 0)
                        break;
                    int px = cursor + 1 + rng.Range(0, room);
                    layout.Buildings.Add(new BuildingPlacement { X = px, GroundY = ground, Spec = prop, Segment = SegmentAt(layout, px) });
                    Mark(solid, taken, px, prop.Width);
                    spots.RemoveAll(s => !s.Roof && s.X > px - 1f && s.X < px + prop.Width + 1f);
                    cursor = px + prop.Width;
                }
                x = Mathf.Max(end, x + 1);
            }
            layout.Buildings.Sort((a, b) => a.X.CompareTo(b.X)); // left to right: the online index order

            // A fence along any short stretch of street between two buildings ties them together.
            BuildingPlacement left = null;
            foreach (BuildingPlacement b in layout.Buildings)
            {
                if (b.IsProp)
                    continue;
                int from = left != null ? left.X + left.Spec.Width : int.MaxValue, gap = b.X - from;
                if (left != null && gap > 0 && gap <= MaxFencedGap && left.GroundY == b.GroundY)
                {
                    bool level = true;
                    for (int x = from; x < b.X; x++)
                        level &= layout.GroundAt(x) == b.GroundY;
                    for (int x = from; level && x < b.X; x++)
                        if (!solid[x])
                        {
                            AddDecor(layout, rng, DecorKind.Fence, x, b.GroundY);
                            solid[x] = true; // dressed
                        }
                }
                left = b;
            }

            int nextLamp = 0;
            for (int x = 0; x < layout.Width; x++)
            {
                int ground = layout.GroundAt(x);
                if (ground == LevelLayout.NoGround || solid[x] || Mathf.Abs(x - layout.FinishX) <= 1)
                    continue;
                if (x >= nextLamp)
                {
                    AddDecor(layout, rng, DecorKind.Lamp, x, ground);
                    nextLamp = x + rng.Range(9, 13);
                }
                else if (rng.Chance(0.25f))
                {
                    var kind = (DecorKind)rng.Range((int)DecorKind.Fence, (int)DecorKind.Sign);
                    for (int run = kind == DecorKind.Fence ? rng.Range(2, 4) : 1; run > 0; run--, x++)
                    {
                        if (x >= layout.Width || solid[x] || layout.GroundAt(x) != ground)
                            break;
                        AddDecor(layout, rng, kind, x, ground);
                    }
                }
            }
        }

        private static List<string> PropKinds(LevelSpec spec)
        {
            var kinds = new List<string>();
            foreach (string name in spec.props != null && spec.props.Length > 0 ? spec.props : DefaultProps)
                if (Array.IndexOf(new[] { "crate", "car", "barrel", "container" }, name) >= 0)
                    kinds.Add(name);
            return kinds;
        }

        /// <summary>One street prop as a tiny building: a pile of crates, a car, a row of gas
        /// tanks (they set each other off), a container (sometimes two stacked).</summary>
        public static BuildingSpec Prop(string name, Pcg32 rng)
        {
            var spec = new BuildingSpec(PropPrefix + name, (int)rng.Next());
            switch (name)
            {
                case "crate":
                    int crates = rng.Range(1, 3);
                    for (int i = 0; i < crates; i++)
                        spec.TryPlace(TileKind.Crate, i, 0, (int)(rng.Next() & 0xFF));
                    for (int i = 0; i < crates - 1; i++)
                        if (rng.Chance(0.6f))
                            spec.TryPlace(TileKind.Crate, i, 1, (int)(rng.Next() & 0xFF));
                    break;
                case "barrel":
                    for (int i = 0, n = rng.Range(1, 3); i < n; i++)
                        spec.TryPlace(TileKind.Barrel, i, 0, (int)(rng.Next() & 0xFF));
                    break;
                case "car":
                    spec.TryPlace(TileKind.Car, 0, 0, (int)(rng.Next() & 0xFF));
                    break;
                default: // container
                    spec.TryPlace(TileKind.Container, 0, 0, (int)(rng.Next() & 0xFF));
                    if (rng.Chance(0.35f))
                        spec.TryPlace(TileKind.Container, 0, 2, (int)(rng.Next() & 0xFF));
                    break;
            }
            return spec;
        }

        private static void Mark(bool[] solid, bool[] taken, int x0, int width)
        {
            for (int x = x0 - 1; x <= x0 + width; x++)
            {
                if (x < 0 || x >= taken.Length)
                    continue;
                taken[x] = true;
                if (solid != null && x >= x0 && x < x0 + width)
                    solid[x] = true;
            }
        }

        private static int SegmentAt(LevelLayout layout, int x)
        {
            for (int i = 0; i < layout.Segments.Count; i++)
                if (x >= layout.Segments[i].X0 && x < layout.Segments[i].X1)
                    return i;
            return Mathf.Max(0, layout.Segments.Count - 1);
        }

        private static void AddDecor(LevelLayout layout, Pcg32 rng, DecorKind kind, int x, int ground) =>
            layout.Decor.Add(new DecorPlacement
            {
                Kind = kind, X = x, GroundY = ground, Variant = (int)(rng.Next() & 0xFF), Segment = SegmentAt(layout, x),
            });

        // ---------------- Pickups ----------------

        private static void SpendPickups(LevelLayout layout, Pcg32 rng, List<Spot> spots)
        {
            PickupBudget budget = layout.Spec.pickups ?? new PickupBudget();
            var free = new List<Spot>(spots);
            Place(layout, rng, free, PickupKind.Axe, 25, budget.axe, roofs: false, nearBuilding: true);
            Place(layout, rng, free, PickupKind.WreckingBall, 20, budget.wreckingBall, roofs: false, nearBuilding: true);
            Place(layout, rng, free, PickupKind.Resurrect, 1, budget.resurrect, roofs: false, nearBuilding: false);
            Place(layout, rng, free, PickupKind.Dynamite, 2, budget.dynamite, roofs: true, nearBuilding: false);
        }

        /// <summary>Columns before a building where a weapon still reads as "for this one".</summary>
        public const int WeaponLeadIn = 6;

        private static bool JustBeforeBuilding(LevelLayout layout, Spot spot)
        {
            foreach (BuildingPlacement b in layout.Buildings)
                if (!b.IsProp && spot.X < b.X && spot.X >= b.X - WeaponLeadIn)
                    return true;
            return false;
        }

        // Spread evenly: the n-th pickup comes from the n-th slice of the remaining spots.
        // Weapons take a spot just before a building in their slice when there is one.
        private static void Place(LevelLayout layout, Pcg32 rng, List<Spot> free, PickupKind kind, int amount, int count, bool roofs,
            bool nearBuilding)
        {
            for (int n = 0; n < count; n++)
            {
                var candidates = new List<int>();
                for (int i = 0; i < free.Count; i++)
                    if (roofs || !free[i].Roof)
                        candidates.Add(i);
                if (candidates.Count == 0)
                    return;
                int sliceStart = candidates.Count * n / count;
                int sliceEnd = Mathf.Max(sliceStart, candidates.Count * (n + 1) / count - 1);
                var preferred = new List<int>();
                for (int c = sliceStart; nearBuilding && c <= sliceEnd; c++)
                    if (JustBeforeBuilding(layout, free[candidates[c]]))
                        preferred.Add(candidates[c]);
                int pick = preferred.Count > 0
                    ? preferred[rng.Range(0, preferred.Count - 1)]
                    : candidates[rng.Range(sliceStart, sliceEnd)];
                Spot spot = free[pick];
                free.RemoveAt(pick);
                layout.Pickups.Add(new PickupPlacement { X = spot.X, Y = spot.Y, Kind = kind, Amount = amount, Segment = spot.Segment });
            }
        }
    }
}
