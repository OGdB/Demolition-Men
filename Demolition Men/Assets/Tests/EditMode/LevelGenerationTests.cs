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
        public void AutoPar_IsTheScrollTimeToTheFinish([ValueSource(nameof(Levels))] string path)
        {
            LevelSpec spec = Load(path);
            spec.parTime = 0f;
            LevelLayout layout = LevelGrammar.Generate(spec, ArchetypesFor(path));
            Assert.AreEqual((layout.FinishX - layout.SpawnX) / spec.scrollSpeed, layout.Par, 1e-3f);
            spec.parTime = 42f;
            Assert.AreEqual(42f, layout.Par);
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
    }
}
