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
                        Flat(layout, 14, height);
                        layout.SpawnX = segment.X0 + 4;
                        break;
                    case SegmentKind.Street:
                        Flat(layout, rng.Range(3, 5), height, spots, index);
                        PlaceBuilding(layout, rng, Pick(archetypes, rng, progress, false), height, index, spots);
                        Flat(layout, rng.Range(3, 5), height, spots, index);
                        break;
                    case SegmentKind.Cluster:
                        Flat(layout, 3, height, spots, index);
                        for (int b = 0; b < Mathf.Max(1, count); b++)
                        {
                            PlaceBuilding(layout, rng, Pick(archetypes, rng, progress, false), height, index, spots);
                            Flat(layout, 2, height, spots, index);
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
                        Flat(layout, 16, height);
                        break;
                }
                segment.X1 = layout.Width;
                layout.Segments.Add(segment);
            }

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
                    spots.Add(new Spot { X = layout.Width + 0.5f, Y = height + 0.5f, Segment = segment });
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
            layout.Buildings.Add(new BuildingPlacement { X = x, GroundY = height, Spec = spec, Segment = segment });
            spots.Add(new Spot { X = x + spec.Width * 0.5f, Y = height + spec.Height + 0.5f, Roof = true, Segment = segment });
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

        // ---------------- Pickups ----------------

        private static void SpendPickups(LevelLayout layout, Pcg32 rng, List<Spot> spots)
        {
            PickupBudget budget = layout.Spec.pickups ?? new PickupBudget();
            var free = new List<Spot>(spots);
            Place(layout, rng, free, PickupKind.Axe, 25, budget.axe, roofs: false);
            Place(layout, rng, free, PickupKind.WreckingBall, 20, budget.wreckingBall, roofs: false);
            Place(layout, rng, free, PickupKind.Resurrect, 1, budget.resurrect, roofs: false);
            Place(layout, rng, free, PickupKind.Dynamite, 2, budget.dynamite, roofs: true);
        }

        // Spread evenly: the n-th pickup comes from the n-th slice of the remaining spots.
        private static void Place(LevelLayout layout, Pcg32 rng, List<Spot> free, PickupKind kind, int amount, int count, bool roofs)
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
                int pick = candidates[rng.Range(sliceStart, sliceEnd)];
                Spot spot = free[pick];
                free.RemoveAt(pick);
                layout.Pickups.Add(new PickupPlacement { X = spot.X, Y = spot.Y, Kind = kind, Amount = amount, Segment = spot.Segment });
            }
        }
    }
}
