using System.Collections.Generic;
using System.Linq;
using System.IO;
using Demolition.Generation;
using Demolition.Levels;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>Every shipped level spec must generate a level a player can cross, the same
    /// way every time, with its pickups and a finish past the spawn.</summary>
    public class LevelGenerationTests
    {
        private const string ContentFolder = "Assets/Content";

        private static IEnumerable<string> Levels() =>
            Directory.GetFiles(ContentFolder, "*.json", SearchOption.AllDirectories).Where(p => p.Replace('\\', '/').Contains("/Levels/"));

        private static List<BuildingArchetype> ArchetypesFor(string levelPath)
        {
            string themeFolder = Path.GetDirectoryName(Path.GetDirectoryName(levelPath));
            var list = new List<BuildingArchetype>();
            foreach (string json in Directory.GetFiles(Path.Combine(themeFolder, "Archetypes"), "*.json"))
                list.Add(BuildingArchetype.FromJson(File.ReadAllText(json)));
            return list;
        }

        private static LevelSpec Load(string path) => LevelSpec.FromJson(File.ReadAllText(path));

        [Test]
        public void EveryLevel_IsPassable_Over30Seeds([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            List<BuildingArchetype> archetypes = ArchetypesFor(path);
            for (int seed = 0; seed < 30; seed++)
            {
                LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), archetypes, JumpProfile.Default,
                    out TraversalReport report);
                Assert.IsTrue(report.Passable, $"{spec.id} seed {spec.seed + seed}: {report}");
                Assert.Greater(layout.FinishX, layout.SpawnX + 20, spec.id);
                Assert.Greater(layout.Buildings.Count, 0, spec.id);
            }
        }

        // The job sheet's Chain objective must be doable on every level, the daily's seeds too:
        // some building has a ground block whose loss brings that many cells down in one cascade
        // (what detaches, then what crumbles within the window, as RunTally chains collapses).
        [Test]
        public void EveryLevel_HasACollapseBigEnoughForTheChainJob([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            List<BuildingArchetype> archetypes = ArchetypesFor(path);
            for (int seed = 0; seed < 10; seed++)
            {
                LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed * 7919, spec.id), archetypes);
                int biggest = 0;
                foreach (BuildingPlacement building in layout.Buildings)
                {
                    if (building.IsProp)
                        continue;
                    var baseline = BuildingValidator.Stress(building.Spec.ToStructure());
                    foreach (SpecBlock block in building.Spec.Blocks)
                    {
                        if (block.Rect.yMin > 1)
                            continue;
                        float lost = BuildingValidator.Cascade(building.Spec, baseline, block) * building.Spec.CellCount;
                        biggest = System.Math.Max(biggest, (int)lost - block.Cells().Count()); // the hit block itself isn't a collapse
                        if (biggest >= Run.Objectives.ChainCells)
                            break;
                    }
                    if (biggest >= Run.Objectives.ChainCells)
                        break;
                }
                Assert.GreaterOrEqual(biggest, Run.Objectives.ChainCells, $"{spec.id} seed {spec.seed + seed * 7919}");
            }
        }

        [Test]
        public void SameSeed_SameLevel([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            List<BuildingArchetype> archetypes = ArchetypesFor(path);
            Assert.AreEqual(LevelGrammar.Generate(spec, archetypes).Hash(), LevelGrammar.Generate(spec, archetypes).Hash());
        }

        [Test]
        public void Seeds_Vary([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            List<BuildingArchetype> archetypes = ArchetypesFor(path);
            var hashes = new HashSet<int>();
            for (int seed = 0; seed < 8; seed++)
                hashes.Add(LevelGrammar.Generate(spec.WithSeed(seed, spec.id), archetypes).Hash());
            Assert.Greater(hashes.Count, 6);
        }

        [Test]
        public void Buildings_StandOnFlatGround_AndDoNotOverlap([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            int lastEnd = int.MinValue;
            foreach (BuildingPlacement b in layout.Buildings)
            {
                Assert.GreaterOrEqual(b.X, lastEnd, $"{spec.id}: buildings overlap at x={b.X}");
                for (int x = b.X; x < b.X + b.Spec.Width; x++)
                    Assert.AreEqual(b.GroundY, layout.GroundAt(x), $"{spec.id}: building at {b.X} not on flat ground at x={x}");
                lastEnd = b.X + b.Spec.Width;
            }
        }

        [Test]
        public void PickupBudget_IsSpent([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            int Count(PickupKind kind) => layout.Pickups.Count(p => p.Kind == kind);
            Assert.AreEqual(spec.pickups.dynamite, Count(PickupKind.Dynamite), spec.id);
            Assert.AreEqual(spec.pickups.axe, Count(PickupKind.Axe), spec.id);
            Assert.AreEqual(spec.pickups.wreckingBall, Count(PickupKind.WreckingBall), spec.id);
            Assert.AreEqual(spec.pickups.resurrect, Count(PickupKind.Resurrect), spec.id);
            foreach (PickupPlacement p in layout.Pickups)
                Assert.That(p.X, Is.InRange(layout.SpawnX, layout.FinishX), $"{spec.id}: {p.Kind} outside the course");
        }

        [Test]
        public void AutoPar_IsAShareOfTheScrollTimeToTheFinish([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            spec.parTime = 0f;
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            Assert.AreEqual(LevelLayout.ParShare * (layout.FinishX - layout.SpawnX) / spec.scrollSpeed, layout.Par, 1e-3f);
            spec.parTime = 42f;
            Assert.AreEqual(42f, layout.Par);
        }

        private static LevelLayout Designed(string[] pacing, int seed, PickupBudget pickups = null)
        {
            string town = Path.Combine(ContentFolder, "Town", "Levels", "town-01.json");
            var spec = new LevelSpec { id = "test", seed = seed, pacing = pacing, pickups = pickups ?? new PickupBudget() };
            return LevelGrammar.Generate(spec, ArchetypesFor(town));
        }

        [Test]
        public void Islands_PutAPitAndALandingStripBetweenBuildings()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "islands:3", "finish" }, seed);
                LevelSegment islands = layout.Segments.First(s => s.Kind == SegmentKind.Islands);
                var inside = layout.Buildings.Where(b => !b.IsProp && b.Segment == layout.Segments.IndexOf(islands)).ToList();
                Assert.AreEqual(3, inside.Count, $"seed {seed}");
                foreach (BuildingPlacement b in inside)
                {
                    int end = b.X + b.Spec.Width;
                    Assert.AreNotEqual(LevelLayout.NoGround, layout.GroundAt(end), $"seed {seed}: a ledge after the building");
                    Assert.AreEqual(LevelLayout.NoGround, layout.GroundAt(end + 1), $"seed {seed}: then a pit");
                }
                for (int i = 1; i < inside.Count; i++)
                    for (int x = inside[i].X - 2; x < inside[i].X; x++)
                        Assert.AreNotEqual(LevelLayout.NoGround, layout.GroundAt(x), $"seed {seed}: landing strip before building {i}");
            }
        }

        [Test]
        public void Plateau_PutsABuildingOnHighGround_AndComesBackDown()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "plateau", "finish" }, seed);
                int street = layout.GroundAt(layout.SpawnX);
                BuildingPlacement b = layout.Buildings.Single(p => !p.IsProp);
                Assert.Greater(b.GroundY, street, $"seed {seed}");
                Assert.AreEqual(street, layout.GroundAt(layout.FinishX), $"seed {seed}");
            }
        }

        [Test]
        public void Weapons_WaitJustBeforeABuilding_WhenTheirSliceHasOne()
        {
            var weapons = new PickupBudget { dynamite = 0, axe = 1, wreckingBall = 1, resurrect = 0 };
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "street", "breather", "cluster:2", "finish" }, seed, weapons);
                foreach (PickupPlacement p in layout.Pickups)
                    Assert.IsTrue(layout.Buildings.Any(b => p.X < b.X && p.X >= b.X - LevelGrammar.WeaponLeadIn),
                        $"seed {seed}: {p.Kind} at {p.X} isn't just before a building");
            }
        }

        [Test]
        public void Clusters_AreTerraces_WallToWall()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "cluster:3", "finish" }, seed, new PickupBudget());
                var row = layout.Buildings.Where(b => !b.IsProp).ToList();
                Assert.AreEqual(3, row.Count, $"seed {seed}");
                for (int i = 1; i < row.Count; i++)
                    Assert.AreEqual(row[i - 1].X + row[i - 1].Spec.Width, row[i].X, $"seed {seed}: building {i} not wall to wall");
            }
        }

        [Test]
        public void Terrace_SharesOneRoofline_WallToWall()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "terrace:3", "finish" }, seed, new PickupBudget());
                var row = layout.Buildings.Where(b => !b.IsProp).ToList();
                Assert.AreEqual(3, row.Count, $"seed {seed}");
                int Roof(BuildingSpec s) => s.Blocks.Where(b => b.Kind != TileKind.Chimney).Max(b => b.Rect.yMax);
                for (int i = 1; i < row.Count; i++)
                {
                    Assert.AreEqual(row[i - 1].X + row[i - 1].Spec.Width, row[i].X, $"seed {seed}: building {i} not wall to wall");
                    Assert.AreEqual(Roof(row[0].Spec), Roof(row[i].Spec), $"seed {seed}: building {i} breaks the roofline");
                    Assert.AreEqual(row[0].Spec.Archetype, row[i].Spec.Archetype, $"seed {seed}: building {i} is another archetype");
                }
            }
        }

        [Test]
        public void Yard_LinesUpBreakables_OnTheStreet()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = Designed(new[] { "start", "street", "yard:4", "finish" }, seed, new PickupBudget());
                LevelSegment yard = layout.Segments.First(s => s.Kind == SegmentKind.Yard);
                var inside = layout.Buildings.Where(b => b.Segment == layout.Segments.IndexOf(yard)).ToList();
                Assert.GreaterOrEqual(inside.Count, 4, $"seed {seed}");
                Assert.IsTrue(inside.All(b => b.IsProp), $"seed {seed}: a building in the yard");
                for (int i = 1; i < inside.Count; i++)
                    Assert.GreaterOrEqual(inside[i].X, inside[i - 1].X + inside[i - 1].Spec.Width + 1, $"seed {seed}: props {i - 1} and {i} touch");
            }
        }

        [Test]
        public void Props_StandClear_OnFlatGround_AwayFromTheStartAndTheFinish([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            int total = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), ArchetypesFor(path));
                var props = layout.Buildings.Where(b => b.IsProp).ToList();
                total += props.Count;
                foreach (BuildingPlacement p in props)
                {
                    Assert.Greater(p.X, layout.SpawnX + 6, $"{spec.id}: {p.Spec.Archetype} at the start");
                    Assert.Less(p.X + p.Spec.Width, layout.FinishX - 1, $"{spec.id}: {p.Spec.Archetype} on the finish");
                    for (int x = p.X - 1; x <= p.X + p.Spec.Width; x++)
                        Assert.AreEqual(p.GroundY, layout.GroundAt(x), $"{spec.id}: {p.Spec.Archetype} at {p.X} needs flat ground and a free column either side");
                    foreach (BuildingPlacement other in layout.Buildings)
                        if (other != p)
                            Assert.IsTrue(other.X + other.Spec.Width < p.X || other.X > p.X + p.Spec.Width,
                                $"{spec.id}: {p.Spec.Archetype} at {p.X} touches {other.Spec.Archetype} at {other.X}");
                }
                foreach (PickupPlacement pickup in layout.Pickups)
                    Assert.IsFalse(props.Any(p => pickup.X > p.X && pickup.X < p.X + p.Spec.Width && pickup.Y < p.GroundY + p.Spec.Height),
                        $"{spec.id}: {pickup.Kind} inside a prop");
            }
            Assert.GreaterOrEqual(total, 15, $"{spec.id}: too few props over 10 seeds");
        }

        [Test]
        public void Decor_StandsOnTheGround_NeverInsideABuildingOrProp([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            Assert.Greater(layout.Decor.Count(d => d.Kind == DecorKind.Lamp), 3, spec.id);
            foreach (DecorPlacement d in layout.Decor)
            {
                Assert.AreEqual(layout.GroundAt(d.X), d.GroundY, $"{spec.id}: {d.Kind} at {d.X} floats or sinks");
                Assert.IsFalse(layout.Buildings.Any(b => d.X >= b.X && d.X < b.X + b.Spec.Width), $"{spec.id}: {d.Kind} at {d.X} inside a building");
            }
        }

        [Test]
        public void Caravan_ParksOnFlatStreetPastTheFinish_WithNothingInFront([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            int x0 = layout.CaravanX, x1 = x0 + LevelLayout.CaravanWidth;
            Assert.Greater(x0, layout.FinishX, $"{spec.id}: the caravan stands on the finish line");
            Assert.LessOrEqual(x1, layout.Width, $"{spec.id}: the caravan sticks out of the level");
            for (int x = x0; x < x1; x++)
                Assert.AreEqual(layout.GroundAt(layout.FinishX), layout.GroundAt(x), $"{spec.id}: column {x} under the caravan isn't street");
            Assert.IsFalse(layout.Decor.Any(d => d.X >= x0 && d.X < x1), $"{spec.id}: dressing in front of the caravan");
            Assert.IsFalse(layout.Buildings.Any(b => b.X < x1 && b.X + b.Spec.Width > x0), $"{spec.id}: a building or prop on the caravan");
        }

        [Test]
        public void Start_HasAClearRunUp_LeftOfTheSpawn([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            Assert.GreaterOrEqual(layout.SpawnX, LevelGrammar.RunUp, spec.id);
            for (int x = 0; x <= layout.SpawnX; x++)
                Assert.AreEqual(layout.GroundAt(layout.SpawnX), layout.GroundAt(x), $"{spec.id}: column {x} of the run-up isn't street");
            Assert.IsFalse(layout.Buildings.Any(b => b.X <= layout.SpawnX), $"{spec.id}: a building or prop on the run-up");
        }

        // ---------------- Ways up ----------------

        [Test]
        public void PlatedBuildings_HaveALadder_UpAFreeColumnOfStreet([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            for (int seed = 0; seed < 10; seed++)
            {
                LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), ArchetypesFor(path));
                foreach (BuildingPlacement b in layout.Buildings)
                {
                    SpecLadder street = b.Spec.Ladders.FirstOrDefault(l => l.X < 0);
                    if (b.Spec.Plated)
                        Assert.IsNotNull(street, $"{spec.id}: {b.Spec.Name} at {b.X} is plated and has no ladder");
                    if (street == null)
                        continue;
                    Assert.IsFalse(b.IsProp || b.IsGate, $"{spec.id}: a ladder up {b.Spec.Name}");
                    Assert.AreEqual(b.Spec.WayIn(), street.Top, $"{spec.id}: {b.Spec.Name}");
                    Assert.AreEqual(b.GroundY, layout.GroundAt(b.X - 1), $"{spec.id}: {b.Spec.Name}'s ladder stands on no street");
                    Assert.IsFalse(layout.Buildings.Any(o => o != b && b.X - 1 >= o.X && b.X - 1 < o.X + o.Spec.Width),
                        $"{spec.id}: {b.Spec.Name}'s ladder is inside something");
                }
            }
        }

        [Test]
        public void WaysUp_AreStepsAJumpApart_ToTheSill([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            int maxRise = TraversalValidator.MaxRise(JumpProfile.Default);
            for (int seed = 0; seed < 20; seed++)
            {
                LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), ArchetypesFor(path));
                foreach (BuildingPlacement way in layout.Buildings.Where(b => b.IsWay))
                {
                    string what = $"{spec.id} seed {spec.seed + seed}: {way.Spec.Archetype} at {way.X}";
                    Assert.IsTrue(way.IsProp, what);
                    BuildingPlacement to = layout.Buildings.Single(b => b.X == way.X + way.Spec.Width + 1); // one column on: the ladder's
                    Assert.IsTrue(to.Spec.Ladders.Any(l => l.X < 0) && to.GroundY == way.GroundY, what);
                    int top = 0; // each column's top is the last one, or at most a jump higher, or open street
                    for (int x = 0; x < way.Spec.Width; x++)
                    {
                        int height = way.Spec.Blocks.Where(k => k.Rect.xMin <= x && k.Rect.xMax > x).Select(k => k.Rect.yMax).DefaultIfEmpty(0).Max();
                        if (height == 0)
                            continue;
                        Assert.That(height - top, Is.InRange(0, maxRise), what + $": step at column {x}");
                        top = height;
                    }
                    Assert.That(to.Spec.WayIn() - top, Is.InRange(0, maxRise), what + ": from its top to the sill");
                    var structure = way.Spec.ToStructure();
                    Assert.AreEqual(0, BuildingValidator.Validate(way.Spec, new BuildingArchetype()).UnsupportedCells, what + " doesn't stand");
                }
            }
        }

        [Test]
        public void WaysUp_ComeInEveryKind_OverTheCampaign()
        {
            var kinds = new HashSet<string>();
            int ladders = 0;
            foreach (string path in Levels())
                for (int seed = 0; seed < 5; seed++)
                {
                    LevelSpec spec = Load(path);
                    LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), ArchetypesFor(path));
                    kinds.UnionWith(layout.Buildings.Where(b => b.IsWay).Select(b => b.Spec.Archetype));
                    ladders += layout.Buildings.Count(b => b.Spec.Ladders.Any(l => l.X < 0));
                }
            CollectionAssert.AreEquivalent(new[] { LevelGrammar.WayPrefix + "crates", LevelGrammar.WayPrefix + "scaffold" }, kinds);
            Assert.Greater(ladders, 20);
        }

        [Test]
        public void Props_FollowTheSpec_AndContainersComeOnlyWhenAsked()
        {
            var spec = new LevelSpec { id = "test", seed = 3, pacing = new[] { "start", "breather", "breather", "breather", "finish" } };
            string town = Path.Combine(ContentFolder, "Town", "Levels", "town-01.json");
            Assert.IsFalse(LevelGrammar.Generate(spec, ArchetypesFor(town)).Buildings.Any(b => b.Spec.Archetype == "prop.container"));
            spec.props = new[] { "container" };
            var props = LevelGrammar.Generate(spec, ArchetypesFor(town)).Buildings.Where(b => b.IsProp).ToList();
            Assert.Greater(props.Count, 0);
            Assert.IsTrue(props.All(b => b.Spec.Archetype == "prop.container"));
        }

        [Test]
        public void Jump_ClearsTheGrammarsWidestPit_AndTwoCellSteps()
        {
            JumpProfile jump = JumpProfile.Default;
            Assert.That(jump.Apex, Is.InRange(3f, 4.5f));
            Assert.GreaterOrEqual(TraversalValidator.MaxRise(jump), 2);
            Assert.GreaterOrEqual(TraversalValidator.MaxGap(jump, 1), LevelGrammar.MaxPit);
            Assert.AreEqual(-1, TraversalValidator.MaxGap(jump, 10), "a 10-cell wall is out of reach");
        }

        [Test]
        public void LowestGround_SeesTrenchFloors_ButNotPitBottoms()
        {
            var layout = new LevelLayout(new LevelSpec());
            int[] ground = { 0, 0, -3, -3, 0, LevelLayout.NoGround, LevelLayout.NoGround, 0 };
            layout.Ground.AddRange(ground);
            Assert.AreEqual(-3, layout.LowestGroundIn(0, 7), "trench floor");
            Assert.AreEqual(0, layout.LowestGroundIn(4, 7), "a pit has no floor to show");
            Assert.AreEqual(LevelLayout.NoGround, layout.LowestGroundIn(5, 6));
            Assert.AreEqual(0, layout.LowestGroundIn(-5, 1), "clamped to the level");
        }

        [Test]
        public void Validator_RejectsAWallAndAWidePit()
        {
            var spec = new LevelSpec();
            var wall = new LevelLayout(spec) { SpawnX = 1, FinishX = 8 };
            for (int x = 0; x < 10; x++)
                wall.Ground.Add(x < 5 ? 0 : 6);
            Assert.IsFalse(TraversalValidator.Validate(wall, JumpProfile.Default).Passable);

            var pit = new LevelLayout(spec) { SpawnX = 1, FinishX = 18 };
            for (int x = 0; x < 20; x++)
                pit.Ground.Add(x >= 4 && x < 14 ? LevelLayout.NoGround : 0);
            Assert.IsFalse(TraversalValidator.Validate(pit, JumpProfile.Default).Passable);
        }

        [Test]
        public void Gate_ComesDownWhenItsFootingGoes()
        {
            BuildingSpec gate = LevelGrammar.Gate(new Pcg32(5), 6);
            var structure = gate.ToStructure();
            var footing = new List<UnityEngine.Vector2Int> { new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(1, 0) };
            Assert.AreEqual(gate.CellCount - 2, structure.RemoveCells(footing).Count);
        }

        [Test]
        public void Daily_SameDateSameSeed_DifferentDatesDiffer()
        {
            var day = new System.DateTime(2026, 10, 17);
            Assert.AreEqual(Daily.Seed(day), Daily.Seed(day.AddHours(5)));
            Assert.AreNotEqual(Daily.Seed(day), Daily.Seed(day.AddDays(1)));
            Assert.AreEqual(Daily.TemplateIndex(day, 4), Daily.TemplateIndex(day, 4));
            Assert.That(Daily.TemplateIndex(day.AddDays(-400), 4), Is.InRange(0, 3));
        }

        // ---------------- The painted weak point (docs/hit-plan.md F1) ----------------

        [Test]
        public void WeakMarks_AreOffUnlessTheSpecAsks()
        {
            Assert.AreEqual(0, new LevelSpec().weakMarks);
            Assert.AreEqual(0, LevelSpec.FromJson(@"{ ""id"": ""x"", ""pacing"": [""start"", ""street"", ""finish""] }").weakMarks);
            Assert.AreEqual(3, LevelSpec.FromJson(@"{ ""id"": ""x"", ""weakMarks"": 3 }").weakMarks);
        }

        [Test]
        public void WeakMarks_OnlyTheFirstTwoLevelsAsk([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            Assert.AreEqual(spec.id == "town-01" || spec.id == "town-02", spec.weakMarks > 0, spec.id);
        }

        // The X goes on the weak point of the level's first weakMarks buildings, props not counted:
        // the block the level dump calls `weak` (tools/unity-check/LevelDump.cs), in reach from the
        // street, and a weak point as its archetype counts them. No other building, and no
        // building of a level that doesn't ask, carries one.
        [Test]
        public void WeakMark_IsTheWeakPointOfTheLevelsFirstBuildings([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            List<BuildingArchetype> archetypes = ArchetypesFor(path);
            LevelLayout layout = LevelGrammar.Generate(spec, archetypes);
            int buildings = 0, marks = 0;
            for (int i = 0; i < layout.Buildings.Count; i++)
            {
                BuildingPlacement b = layout.Buildings[i];
                SpecBlock mark = layout.WeakMark(i);
                if (b.IsProp || buildings++ >= spec.weakMarks)
                {
                    Assert.IsNull(mark, $"{spec.id}: building {i} ({b.Spec.Archetype})");
                    continue;
                }
                Assert.AreSame(BuildingValidator.WeakPoint(b.Spec, out float cascade), mark, $"{spec.id}: building {i}");
                Assert.IsNotNull(mark, $"{spec.id}: building {i} ({b.Spec.Archetype}) has no weak point to paint");
                Assert.LessOrEqual(mark.Rect.yMin, 1, $"{spec.id}: building {i}");
                BuildingArchetype archetype = archetypes.FirstOrDefault(a => a.id == b.Spec.Archetype); // none: a gate
                Assert.GreaterOrEqual(cascade + 1e-4f, archetype != null ? archetype.weakPointCascade : 1f,
                    $"{spec.id}: building {i} ({b.Spec.Archetype}): the X brings too little down");
                marks++;
            }
            Assert.AreEqual(System.Math.Min(spec.weakMarks, buildings), marks, spec.id);
        }

        // A daily and a ranked run are a campaign level under another seed (LevelSelection.SelectDaily
        // and SelectRanked, the ranked server's Levels): town-01's turn must not bring its paint along.
        [Test]
        public void WeakMarks_NeverInADailyOrARankedRun([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            var date = new System.DateTime(2026, 11, 17);
            Ranked.RankedDay ranked = Ranked.RankedDay.Local(date, spec.id, Ranked.RankedMode.Solo);
            foreach (LevelSpec played in new[] { spec.WithSeed(Daily.Seed(date), Daily.Id(date)), spec.WithSeed(ranked.Seed, ranked.LevelId) })
            {
                Assert.AreEqual(0, played.weakMarks, played.id);
                LevelLayout layout = LevelGrammar.Generate(played, ArchetypesFor(path));
                for (int i = 0; i < layout.Buildings.Count; i++)
                    Assert.IsNull(layout.WeakMark(i), $"{played.id} from {spec.id}: building {i}");
            }
        }
    }
}
