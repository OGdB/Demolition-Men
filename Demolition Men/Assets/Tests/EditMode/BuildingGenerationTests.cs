using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demolition.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    /// <summary>Every shipped archetype must generate buildings that stand, hit their
    /// weak-point targets and are reproducible from the seed.</summary>
    public class BuildingGenerationTests
    {
        private const string ContentFolder = "Assets/Content";

        private static IEnumerable<string> Archetypes() =>
            Directory.GetFiles(ContentFolder, "*.json", SearchOption.AllDirectories).Where(p => p.Replace('\\', '/').Contains("/Archetypes/"));

        private static BuildingArchetype Load(string path) => BuildingArchetype.FromJson(File.ReadAllText(path));

        [Test]
        public void Interior_FillsOpenAndSeeThroughCells_HungFromTheCeiling()
        {
            var spec = new BuildingSpec("test", 1);
            for (int y = 0; y < 3; y++)
            {
                spec.TryPlace(TileKind.Brick, 0, y);
                spec.TryPlace(TileKind.Brick, 3, y);
            }
            spec.TryPlace(TileKind.Support, 1, 1);
            for (int x = 0; x < 4; x++)
                spec.TryPlace(TileKind.Brick, x, 3);
            spec.TryPlace(TileKind.Chimney, 1, 4);

            var interior = spec.Interior().ToDictionary(c => c.Cell);
            Assert.AreEqual(6, interior.Count, "x 1..2, rows 0..2: nothing outside the walls, none on the roof");
            Assert.AreEqual(new Vector2Int(2, 3), interior[new Vector2Int(2, 0)].Host, "an open cell hangs from its ceiling");
            Assert.AreEqual(new Vector2Int(1, 1), interior[new Vector2Int(1, 1)].Host, "a see-through cell hangs from its own block");
            Assert.AreEqual(new Vector2Int(1, 1), interior[new Vector2Int(1, 0)].Host, "the support is the ceiling of the cell below it");
            Assert.IsTrue(interior[new Vector2Int(2, 2)].UnderCeiling);
            Assert.IsFalse(interior[new Vector2Int(2, 1)].UnderCeiling);
        }

        [Test]
        public void Interior_ShowsTheRoomThroughWindows_HungFromTheLintel()
        {
            // Walls at x 0 and 3, a window in the right wall at y 1..2, a roof at y 4.
            var spec = new BuildingSpec("test", 1);
            for (int y = 0; y < 4; y++)
                spec.TryPlace(TileKind.Brick, 0, y);
            spec.TryPlace(TileKind.Brick, 3, 0);
            spec.TryPlace(TileKind.Glass, 3, 1);
            spec.TryPlace(TileKind.Brick, 3, 3);
            for (int x = 0; x < 4; x++)
                spec.TryPlace(TileKind.Brick, x, 4);

            var interior = spec.Interior().ToDictionary(c => c.Cell);
            Assert.AreEqual(10, interior.Count, "the room (x 1..2, rows 0..3) and both window cells, though the window is the outer wall");
            Assert.IsFalse(interior.ContainsKey(new Vector2Int(0, 1)), "a plain outer wall shows no back wall");
            Assert.AreEqual(new Vector2Int(3, 3), interior[new Vector2Int(3, 1)].Host, "a window hangs from its lintel, so a broken one still shows the room");
            Assert.AreEqual(new Vector2Int(3, 3), interior[new Vector2Int(3, 2)].Host);
            Assert.IsTrue(interior[new Vector2Int(3, 2)].UnderCeiling);
            Assert.IsFalse(interior[new Vector2Int(3, 1)].UnderCeiling, "under the pane's own top half");
            Assert.IsTrue(interior[new Vector2Int(3, 1)].SeeThrough, "no pictures behind a pane");
        }

        [Test]
        public void Edges_AreTheOutermostBlocksOfTheirRows_AndStoreysCountSlabRows()
        {
            // Two storeys: walls at x 0 and 4, a plank slab at y 3, an inset upper left wall.
            var spec = new BuildingSpec("test", 1);
            for (int y = 0; y < 3; y++)
            {
                spec.TryPlace(TileKind.Brick, 0, y);
                spec.TryPlace(TileKind.Brick, 4, y);
            }
            spec.TryPlace(TileKind.Support, 2, 1);
            for (int x = 1; x < 4; x++)
                spec.TryPlace(TileKind.Plank, x, 3);
            spec.TryPlace(TileKind.BrickTall, 1, 4);
            spec.TryPlace(TileKind.Brick, 4, 4);
            spec.TryPlace(TileKind.Brick, 4, 5);

            BlockEdge Edge(int x, int y) => spec.EdgeOf(spec.At(new Vector2Int(x, y)));
            Assert.AreEqual(BlockEdge.Left, Edge(0, 0));
            Assert.AreEqual(BlockEdge.Right, Edge(4, 2));
            Assert.AreEqual(BlockEdge.None, Edge(2, 1), "an interior support is no edge");
            Assert.AreEqual(BlockEdge.Left, Edge(1, 4), "the inset upper wall is the left edge of its rows");
            Assert.AreEqual(BlockEdge.None, Edge(2, 3), "a slab piece between walls");

            var interior = spec.Interior().ToDictionary(c => c.Cell);
            Assert.AreEqual(0, interior[new Vector2Int(1, 1)].Storey);
            Assert.AreEqual(1, interior[new Vector2Int(2, 4)].Storey, "above the slab");
            Assert.IsTrue(interior[new Vector2Int(2, 1)].SeeThrough, "behind the support");
            Assert.IsFalse(interior[new Vector2Int(1, 1)].SeeThrough);
        }

        [Test]
        public void EveryArchetype_StandsAndMeetsTargets_Over200Seeds([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            for (int seed = 0; seed < 200; seed++)
            {
                BuildingGrammar.GenerateValid(archetype, seed, out BuildingReport report);
                Assert.IsTrue(report.MeetsTargets, $"{archetype.id} seed {seed}: {report}");
            }
        }

        [Test]
        public void SameSeed_SameBuilding([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            for (int seed = 0; seed < 20; seed++)
            {
                int a = BuildingGrammar.GenerateValid(archetype, seed, out _).Hash();
                int b = BuildingGrammar.GenerateValid(archetype, seed, out _).Hash();
                Assert.AreEqual(a, b, $"{archetype.id} seed {seed}");
            }
        }

        [Test]
        public void DifferentSeeds_GiveVariety([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            var hashes = new HashSet<int>();
            for (int seed = 0; seed < 20; seed++)
                hashes.Add(BuildingGrammar.GenerateValid(archetype, seed, out _).Hash());
            Assert.GreaterOrEqual(hashes.Count, 15, archetype.id);
        }

        [Test]
        public void Validator_FlagsAFloatingBlock()
        {
            var spec = new BuildingSpec("test", 0);
            spec.TryPlace(TileKind.Brick, 0, 0);
            spec.TryPlace(TileKind.Brick, 3, 2); // touches nothing

            BuildingReport report = BuildingValidator.Validate(spec, new BuildingArchetype());

            Assert.AreEqual(1, report.UnsupportedCells);
            Assert.IsFalse(report.MeetsTargets);
        }

        [Test]
        public void Validator_ColumnBase_IsAWeakPoint_OfAHangingSlab()
        {
            // A single column holding a slab: its base brings everything down.
            var spec = new BuildingSpec("test", 0);
            for (int y = 0; y < 4; y++)
                spec.TryPlace(TileKind.Support, 2, y);
            for (int x = 0; x < 5; x++)
                spec.TryPlace(TileKind.Plank, x, 4);

            BuildingReport report = BuildingValidator.Validate(spec, new BuildingArchetype());

            // Both ground-level column cells (rows 0 and 1) take the slab with them.
            Assert.AreEqual(2, report.WeakPoints.Count);
            Assert.AreEqual(new Vector2Int(2, 0), report.WeakPoints[0].Rect.position);
            Assert.AreEqual(1f, report.MaxCascade, 1e-5f);
        }

        // ---------------- Furnishing ----------------

        [Test]
        public void Furnishing_PutsEveryPropWhereItBelongs([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            for (int seed = 0; seed < 40; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, seed, out _);
                var room = new HashSet<Vector2Int>(spec.Interior().Where(c => !c.SeeThrough).Select(c => c.Cell));
                var taken = new HashSet<Vector2Int>();
                Assert.IsNotEmpty(spec.Props, $"{spec.Name}: every archetype furnishes its rooms");
                foreach (SpecProp prop in spec.Props)
                {
                    string what = $"{spec.Name}: {prop.Name} at {prop.Rect}";
                    Assert.IsTrue(Furnishing.TryGet(prop.Name, out PropInfo info), what + " is not in the catalog");
                    Assert.AreEqual(new Vector2Int(info.Width, info.Height), prop.Rect.size, what);
                    Assert.IsNotNull(spec.At(prop.Host), what + " goes with no block");
                    for (int x = prop.Rect.xMin; x < prop.Rect.xMax; x++)
                    {
                        for (int y = prop.Rect.yMin; y < prop.Rect.yMax; y++)
                        {
                            Assert.IsTrue(taken.Add(new Vector2Int(x, y)), what + " overlaps another prop");
                            Assert.IsFalse(spec.Ladders.Any(l => l.X == x && y >= l.Y && y < l.Top), what + " stands in a ladder's way");
                            Assert.IsTrue(info.Slot == PropSlot.Roof ? spec.IsFree(x, y) : room.Contains(new Vector2Int(x, y)), what + " is not in a room");
                        }
                        if (info.Slot == PropSlot.Floor || info.Slot == PropSlot.Roof)
                            Assert.IsTrue(prop.Rect.yMin == 0 || !spec.IsFree(x, prop.Rect.yMin - 1), what + " stands on nothing");
                        if (info.Slot == PropSlot.Ceiling)
                            Assert.IsFalse(spec.IsFree(x, prop.Rect.yMax), what + " hangs from nothing");
                    }
                }
            }
        }

        [Test]
        public void Furnishing_AClubLooksLikeAClub()
        {
            BuildingArchetype club = Load(Archetypes().Single(p => p.Replace('\\', '/').EndsWith("Neon/Archetypes/club.json")));
            for (int seed = 0; seed < 100; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(club, seed, out _);
                foreach (string name in new[] { "bar", "speaker", "djdesk", "discoball", "sign_club" })
                    Assert.IsTrue(spec.Props.Any(p => p.Name == name), $"{spec.Name} has no {name}");
                Assert.IsTrue(spec.Props.Where(p => p.Name == "bar").All(p => p.Storey == 0), "the bar is downstairs");
                Assert.IsTrue(spec.LitStoreys.Count > 0 && spec.Glows(spec.Props.First(p => p.Name == "discoball")), "a club is lit");
            }
        }

        [Test]
        public void Furnishing_NeverChangesABlock([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype furnished = Load(path), bare = Load(path);
            bare.furnish = new FurnishPlan();
            for (int seed = 0; seed < 20; seed++)
            {
                BuildingSpec spec = BuildingGrammar.Generate(bare, seed);
                Assert.IsEmpty(spec.Props);
                Assert.AreEqual(spec.Hash(), BuildingGrammar.Generate(furnished, seed).Hash(), $"{furnished.id} seed {seed}");
            }
        }

        [Test]
        public void Furnishing_EveryPropHasItsSprite([ValueSource(nameof(Archetypes))] string path)
        {
            FurnishPlan plan = Load(path).furnish;
            string sprites = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path)), "Sprites");
            foreach (string name in plan.any.Concat(plan.ground).Concat(plan.upper).Append(plan.roof).Where(n => !string.IsNullOrEmpty(n)))
            {
                Assert.IsTrue(Furnishing.TryGet(name, out PropInfo info), $"{path}: {name} is not in the catalog");
                string png = Path.Combine(sprites, $"prop_{name}.png");
                Assert.IsTrue(File.Exists(png) && File.Exists(png + ".meta"), $"{png} (and its .meta): run tools/artpipe/build_theme.py");
                byte[] header = File.ReadAllBytes(png).Take(24).ToArray(); // PNG: width and height at bytes 16 and 20, big-endian
                Assert.AreEqual(new Vector2Int(info.Width * 32, info.Height * 32),
                    new Vector2Int(header[18] << 8 | header[19], header[22] << 8 | header[23]), png + ": 32 px per cell");
            }
        }

        [Test]
        public void Windows_ComeInEveryLook()
        {
            BuildingArchetype club = Load(Archetypes().Single(p => p.Replace('\\', '/').EndsWith("Neon/Archetypes/club.json")));
            var looks = new HashSet<int>();
            for (int seed = 0; seed < 60; seed++)
                foreach (SpecBlock block in BuildingGrammar.GenerateValid(club, seed, out _).Blocks)
                    if (block.Kind == TileKind.Glass)
                        looks.Add(block.Variant);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, looks, "plain, blinds, boarded up");
        }

        // ---------------- Ladders and plated fronts ----------------

        [Test]
        public void Ladders_RunUpFreeCells_AndAreFixedToABlock([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            for (int seed = 0; seed < 40; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, seed, out _);
                spec.AddStreetLadder();
                foreach (SpecLadder ladder in spec.Ladders)
                {
                    string what = $"{spec.Name}: ladder at x {ladder.X}, {ladder.Y} to {ladder.Top}";
                    Assert.Greater(ladder.Top, ladder.Y + 3, what + " leads nowhere");
                    Assert.IsNotNull(spec.At(ladder.Host), what + " is fixed to nothing");
                    Assert.AreEqual(1, System.Math.Abs(ladder.Host.x - ladder.X), what + ": fixed to the block beside its top");
                    Assert.IsTrue(ladder.Y == 0 || !spec.IsFree(ladder.X, ladder.Y - 1) || spec.Ladders.Any(l => l.X == ladder.X && l.Top == ladder.Y),
                        what + " starts in mid-air (on a floor, or where the ladder below it ends)");
                    for (int y = ladder.Y; y < ladder.Top + 2; y++)
                        Assert.IsTrue(ladder.X < 0 || spec.IsFree(ladder.X, y), what + $": blocked at row {y}");
                    // Where it ends, a floor to step onto: beside the top of a stairwell's, the sill or the roof for the street's.
                    Assert.IsFalse(spec.IsFree(ladder.Host.x, ladder.Top - 1), what);
                    SpecBlock over = spec.At(new Vector2Int(ladder.Host.x, ladder.Top));
                    Assert.IsTrue(over == null || over.Kind == TileKind.Glass || TileKinds.WalkThrough(over.Kind), what + ": a wall where it ends");
                }
            }
        }

        [Test]
        public void Roof_ClosesOverTheWalls_AndOnlyTheStackGrowsTall([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            TileKind roof = BuildingArchetype.ParseTile(archetype.roof, TileKind.Beam);
            TileKind wall = BuildingArchetype.ParseTile(archetype.wall, TileKind.Brick);
            for (int seed = 0; seed < 40; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, seed, out _);
                int roofY = spec.Blocks.Where(b => b.Kind != TileKind.Chimney).Max(b => b.Rect.yMax) - 1;
                List<SpecBlock> row = spec.Blocks.Where(b => b.Rect.y == roofY && b.Kind != TileKind.Chimney)
                    .OrderBy(b => b.Rect.x).ToList();
                foreach (SpecBlock b in row)
                    Assert.IsTrue(b.Kind == roof || b.Kind == wall, $"{spec.Name}: a {b.Kind} at x {b.Rect.x} in the roof (neither roof nor wall)");
                // A floor board leaves the lower half of its cell open: over a wall that is a gap.
                Assert.IsFalse(TileKinds.SeeThrough(row[0].Kind), $"{spec.Name}: the roof doesn't close over the left wall");
                Assert.IsFalse(TileKinds.SeeThrough(row[row.Count - 1].Kind), $"{spec.Name}: the roof doesn't close over the right wall");

                foreach (SpecBlock b in spec.Blocks.Where(b => b.Kind == TileKind.Chimney))
                    if (spec.At(new Vector2Int(b.Rect.x, b.Rect.y - 1))?.Kind == TileKind.Chimney)
                        Assert.AreEqual(0, b.Variant, $"{spec.Name}: a tank, vent, AC unit or sign box stacked on another");
            }
        }

        [Test]
        public void EveryTheme_HasAPlatedArchetype()
        {
            foreach (string theme in Archetypes().Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p))).Distinct())
                Assert.IsTrue(Archetypes().Any(p => p.StartsWith(theme) && Load(p).Plated), theme);
        }

        [Test]
        public void PlatedFront_HasNoWayInAtStreetLevel_AndAWindowOverIt([ValueSource(nameof(Archetypes))] string path)
        {
            BuildingArchetype archetype = Load(path);
            if (!archetype.Plated)
            {
                Assert.IsFalse(BuildingGrammar.GenerateValid(archetype, 1, out _).Plated);
                return;
            }
            Assert.Greater(archetype.columnSpacing.min, 0, "the window over the shutter hangs from a slab the supports hold");
            Assert.GreaterOrEqual(archetype.floors.min, 2, "the way in is the first floor");
            for (int seed = 0; seed < 100; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, seed, out _);
                Assert.IsTrue(spec.Plated, spec.Name);
                int sill = spec.WayIn();
                Assert.That(sill, Is.InRange(5, BuildingSpec.HighestWayIn), spec.Name);
                for (int y = 0; y < sill - 1; y++)
                    Assert.AreEqual(TileKind.Steel, spec.At(new Vector2Int(0, y)).Kind, $"{spec.Name}: row {y} of the front");
                Assert.AreEqual(TileKind.Glass, spec.At(new Vector2Int(0, sill)).Kind, $"{spec.Name}: the window over the shutter");
                Assert.AreEqual(TileKind.Door, spec.At(new Vector2Int(spec.Width - 1, 0)).Kind, $"{spec.Name}: the way out, in the far wall");
                Assert.IsTrue(spec.AddStreetLadder() && spec.Ladders.Last().Top == sill, spec.Name);
            }
        }

        // ---------------- Power cut ----------------

        [Test]
        public void PowerCut_TripsOnOneBigCollapse_OrOnceEnoughIsGone()
        {
            Assert.IsFalse(PowerCut.Trips(100, 5, 5), "a few blocks");
            Assert.IsFalse(PowerCut.Trips(20, 3, 3), "a small building losing three cells is no collapse");
            Assert.IsTrue(PowerCut.Trips(100, 15, 15), "15% in one go");
            Assert.IsFalse(PowerCut.Trips(100, 39, 1), "bit by bit, not yet");
            Assert.IsTrue(PowerCut.Trips(100, 40, 1), "bit by bit, 40% gone");
            Assert.IsFalse(PowerCut.Trips(0, 0, 0));
        }

        [Test]
        public void PowerCut_RunsOutFromTheBreak_NeverAllAtOnce_NeverLong()
        {
            var from = new Vector2(2f, 1f);
            var delays = new List<float>();
            for (int lamp = 0; lamp < 30; lamp++)
            {
                uint hash = PowerCut.Hash(7, lamp);
                float near = PowerCut.Delay(new Vector2(3f, 1f), from, hash), far = PowerCut.Delay(new Vector2(12f, 9f), from, hash);
                Assert.Less(near, far, "the lamp at the break dies before the one at the far end");
                Assert.LessOrEqual(far, PowerCut.MaxSeconds);
                Assert.LessOrEqual(PowerCut.Flickers(hash), PowerCut.MaxFlickers);
                delays.Add(near);
            }
            Assert.Greater(delays.Distinct().Count(), 20, "lamps side by side don't die in the same frame");
            Assert.LessOrEqual(PowerCut.Delay(new Vector2(500f, 0f), from, 0xFF), PowerCut.MaxSeconds);
        }
    }
}
