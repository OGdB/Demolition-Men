using System;
using Demolition.Generation;
using Demolition.Levels;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>The rubble card (docs/hit-plan.md F2): the text SHARE copies from the results.</summary>
    public class RubbleCardTests
    {
        private static readonly float[] Street = { 1f, 0.9f, 0.5f, 0.85f, 0.1f, 0.8f, 1f, 0.4f };

        [Test]
        public void ADaily_ReadsAsThePlansCard_WithoutTheStreakTheGameDoesNotHave()
        {
            string card = RubbleCard.Text(RubbleCard.DailyTitle(new DateTime(2026, 11, 12)), 0.825f, false, 102.7f, Street, "https://example.org/play");

            Assert.AreEqual("DEMOLITION MEN · Daily 12 Nov\n82% ★★★ · 1:42\n💥💥🏚️💥🏢💥💥🏚️\nplay it: https://example.org/play", card);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NoLinkConfigured_NoLinkLine(string link)
        {
            string card = RubbleCard.Text("MAIN STREET", 0.825f, false, 102.7f, Street, link);

            Assert.AreEqual("DEMOLITION MEN · MAIN STREET\n82% ★★★ · 1:42\n💥💥🏚️💥🏢💥💥🏚️", card);
        }

        [Test]
        public void Marks_ChangeAtTheirThresholds()
        {
            Assert.AreEqual(RubbleCard.Standing, RubbleCard.Mark(0f));
            Assert.AreEqual(RubbleCard.Standing, RubbleCard.Mark(RubbleCard.HalfDownAt - 0.001f));
            Assert.AreEqual(RubbleCard.HalfDown, RubbleCard.Mark(RubbleCard.HalfDownAt));
            Assert.AreEqual(RubbleCard.HalfDown, RubbleCard.Mark(RubbleCard.FlattenedAt - 0.001f));
            Assert.AreEqual(RubbleCard.Flattened, RubbleCard.Mark(RubbleCard.FlattenedAt));
            Assert.AreEqual(RubbleCard.Flattened, RubbleCard.Mark(1f));
        }

        // If these move, the plan's legend and CLAUDE.md say something else than the card does.
        [Test]
        public void Thresholds_AreTheFirstAndTheThirdStars()
        {
            Assert.AreEqual(0.4f, RubbleCard.HalfDownAt);
            Assert.AreEqual(0.8f, RubbleCard.FlattenedAt);
            Assert.AreEqual(RunScore.StarThreshold(0), RubbleCard.HalfDownAt);
            Assert.AreEqual(RunScore.StarThreshold(RunScore.MaxStars - 1), RubbleCard.FlattenedAt);
        }

        [TestCase(0.395f, true, "39% ☆☆☆")] // no stars, so nothing to turn rainbow
        [TestCase(0.455f, false, "45% ★☆☆")]
        [TestCase(0.655f, true, "65% ★★☆🌈")]
        [TestCase(1f, true, "100% ★★★🌈")]
        [TestCase(1f, false, "100% ★★★")]
        public void Stars_AreTheResultsScreens_RainbowWithinPar(float destroyed, bool withinPar, string expected)
        {
            string line = RubbleCard.Text("X", destroyed, withinPar, 0f, Street, null).Split('\n')[1];

            Assert.AreEqual(expected + " · 0:00", line);
        }

        [TestCase(0f, "0:00")]
        [TestCase(59.9f, "0:59")] // whole seconds, never a faster time than was run
        [TestCase(60f, "1:00")]
        [TestCase(605.2f, "10:05")]
        public void Time_IsMinutesAndWholeSeconds(float seconds, string expected)
        {
            string line = RubbleCard.Text("X", 0f, false, seconds, Street, null).Split('\n')[1];

            StringAssert.EndsWith(" · " + expected, line);
        }

        [Test]
        public void NoNameAndNoBuildings_LeaveNoEmptyPieces()
        {
            Assert.AreEqual("DEMOLITION MEN\n0% ☆☆☆ · 0:07", RubbleCard.Text(null, 0f, false, 7f, new float[0], null));
        }

        [Test]
        public void Street_IsTheBuildingsInLayoutOrder_WithoutPropsAndGates()
        {
            var layout = new LevelLayout(new LevelSpec());
            layout.Buildings.Add(Placed(5, Block("shed", 2)));
            layout.Buildings.Add(Placed(10, Block(LevelGrammar.PropPrefix + "crate", 1)));
            layout.Buildings.Add(Placed(20, Block("house", 10)));
            layout.Buildings.Add(Placed(30, LevelGrammar.Gate(new Pcg32(1), 5)));
            layout.Buildings.Add(Placed(40, Block("tower", 4)));
            int gate = layout.Buildings[3].Spec.CellCount;

            // The crate and the gate are gone entirely; neither is on the card.
            CollectionAssert.AreEqual(new[] { 0.5f, 0.3f, 1f }, RubbleCard.Street(layout, new[] { 1, 1, 3, gate, 4 }));
        }

        [Test]
        public void Street_NeverPassesAWholeBuilding()
        {
            var layout = new LevelLayout(new LevelSpec());
            layout.Buildings.Add(Placed(0, Block("house", 4)));

            CollectionAssert.AreEqual(new[] { 1f }, RubbleCard.Street(layout, new[] { 5 }));
        }

        private static BuildingPlacement Placed(int x, BuildingSpec spec) => new BuildingPlacement { X = x, Spec = spec };

        // A column of single cells: only the count matters here.
        private static BuildingSpec Block(string archetype, int cells)
        {
            var spec = new BuildingSpec(archetype, 0);
            for (int y = 0; y < cells; y++)
                Assert.IsTrue(spec.TryPlace(TileKind.Brick, 0, y));
            return spec;
        }
    }
}
