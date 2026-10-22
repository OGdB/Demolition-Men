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
    }
}
