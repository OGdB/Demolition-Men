using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demolition.Clips;
using Demolition.Generation;
using Demolition.Levels;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>The Clip Lab's shot files (content-work/shots) and the pure parts of a shot:
    /// checking a shot, framing it, finding a building's weak point.</summary>
    public class ShotTests
    {
        private const string ShotsFolder = "../content-work/shots";
        private const string ContentFolder = "Assets/Content";

        private static IEnumerable<string> Shots() =>
            Directory.Exists(ShotsFolder) ? Directory.GetFiles(ShotsFolder, "*.json").OrderBy(p => p) : Enumerable.Empty<string>();

        private const string OneHit = @"{
            ""id"": ""test"", ""level"": ""town-01"", ""building"": 2,
            ""steps"": [
                { ""p"": 1, ""do"": ""walk"", ""to"": ""weakpoint"" },
                { ""p"": 1, ""do"": ""hit"", ""until"": ""collapse"" },
                { ""p"": 0, ""do"": ""wait"", ""until"": ""collapse"", ""cells"": 20 },
                { ""p"": 0, ""do"": ""slowmo"", ""scale"": 0.3, ""seconds"": 1.5 },
                { ""p"": 0, ""do"": ""wait"", ""seconds"": 4 }
            ]
        }";

        [Test]
        public void Shot_ParsesWithDefaultsAndLanes()
        {
            ShotSpec shot = ShotSpec.FromJson(OneHit);
            CollectionAssert.IsEmpty(shot.Errors());
            Assert.AreEqual(1920, shot.width);
            Assert.AreEqual("frame", shot.camera.mode);
            Assert.AreEqual(2, shot.Lane(1).Count());
            Assert.AreEqual(3, shot.Lane(0).Count());
            Assert.AreEqual(20, shot.Lane(0).First().cells);
        }

        [Test]
        public void Shot_ErrorsNameWhatIsWrong()
        {
            ShotSpec shot = ShotSpec.FromJson(@"{ ""id"": ""bad"", ""level"": ""town-01"", ""width"": 1081,
                ""steps"": [ { ""p"": 2, ""do"": ""hit"" }, { ""p"": 1, ""do"": ""slowmo"" }, { ""p"": 1, ""do"": ""walk"", ""to"": ""weakpoint"" } ] }");
            List<string> errors = shot.Errors();
            Assert.IsTrue(errors.Any(e => e.Contains("1081")), "odd frame size");
            Assert.IsTrue(errors.Any(e => e.Contains("lane 2")), "player 2 in a solo shot");
            Assert.IsTrue(errors.Any(e => e.Contains("not a player step")), "slowmo in a player lane");
            Assert.IsTrue(errors.Any(e => e.Contains("needs a building")), "weakpoint without a building");
            Assert.IsTrue(errors.Any(e => e.Contains("no director step")), "a shot that never ends");
        }

        [Test]
        public void Frame_SquareMasterKeepsTheSubjectInTheMiddleSquare()
        {
            // 10 x 10 in a square frame: it fills the middle 9/16 and stands on its bottom edge.
            ShotPlanning.Frame(0f, 10f, 0f, 10f, 1920, 1920, 1f, out float x, out float y, out float size);
            Assert.AreEqual(5f, x, 1e-4);
            Assert.AreEqual(5f / (9f / 16f), size, 1e-4);
            Assert.AreEqual(0f, y - size * 9f / 16f, 1e-4, "subject bottom on the safe region's bottom");

            // A wide, low subject in 16:9 is fitted by its width; the frame is the safe region.
            ShotPlanning.Frame(0f, 20f, 0f, 5f, 1920, 1080, 1f, out _, out y, out size);
            Assert.AreEqual(10f / (1920f / 1080f), size, 1e-4);
            Assert.AreEqual(size, y, 1e-4);

            ShotPlanning.Frame(0f, 1f, 0f, 1f, 1920, 1080, 5f, out _, out _, out size);
            Assert.AreEqual(5f, size, 1e-4, "never closer than the minimum size");
        }

        [Test]
        public void WeakPoint_IsAGroundLevelBlockThatBringsTheBuildingDown()
        {
            BuildingArchetype rowhouse = BuildingArchetype.FromJson(File.ReadAllText($"{ContentFolder}/Town/Archetypes/rowhouse.json"));
            for (int seed = 0; seed < 20; seed++)
            {
                BuildingSpec spec = BuildingGrammar.GenerateValid(rowhouse, seed, out BuildingReport report);
                SpecBlock weak = ShotPlanning.WeakPoint(spec, out float cascade);
                Assert.IsNotNull(weak, $"seed {seed}");
                Assert.LessOrEqual(weak.Rect.yMin, 1, $"seed {seed}: reachable from the street");
                Assert.AreNotEqual(BlockMaterial.Glass, weak.Material, $"seed {seed}");
                Assert.GreaterOrEqual(cascade + 1e-4f, report.MaxCascade, $"seed {seed}: the biggest cascade the validator found");
            }
        }

        [Test]
        public void ShotFile_Plays([ValueSource(nameof(Shots))] string path)
        {
            ShotSpec shot = ShotSpec.FromJson(File.ReadAllText(path));
            CollectionAssert.IsEmpty(shot.Errors(), path);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(path), shot.id, "the Clip Lab finds shots by file name");

            string levelPath = Directory.GetFiles(ContentFolder, "*.json", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Replace('\\', '/').Contains("/Levels/") && LevelSpec.FromJson(File.ReadAllText(p)).id == shot.level);
            Assert.IsNotNull(levelPath, $"{shot.id}: no campaign level {shot.level}");
            if (shot.building < 0)
                return;

            LevelSpec spec = LevelSpec.FromJson(File.ReadAllText(levelPath));
            if (shot.seed != 0)
                spec = spec.WithSeed(shot.seed, spec.id);
            var archetypes = new List<BuildingArchetype>();
            foreach (string json in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(levelPath)), "Archetypes"), "*.json"))
                archetypes.Add(BuildingArchetype.FromJson(File.ReadAllText(json)));
            LevelLayout layout = LevelGrammar.Generate(spec, archetypes, JumpProfile.Default, out _);
            Assert.Less(shot.building, layout.Buildings.Count, $"{shot.id}: {shot.level} has {layout.Buildings.Count} buildings");
            BuildingPlacement placement = layout.Buildings[shot.building];
            Assert.IsFalse(placement.IsProp && shot.Lane(1).Any(s => s.to == "weakpoint"), $"{shot.id}: building {shot.building} is a {placement.Spec.Archetype}");
            if (shot.steps.Any(s => s.to == "weakpoint"))
                Assert.IsNotNull(ShotPlanning.WeakPoint(placement.Spec, out _), $"{shot.id}: no weak point at ground level");
        }
    }
}
