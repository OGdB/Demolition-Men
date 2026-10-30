using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class RunScoreTests
    {
        [Test]
        public void Destroyed_PaysMaterialPointsPerCell_ToTheInstigator()
        {
            var score = new RunScore(2);

            score.AddDestroyed(1, BlockMaterial.Brick, 4);

            Assert.AreEqual(4 * BlockMaterialInfo.Points(BlockMaterial.Brick), score.Points(1));
            Assert.AreEqual(0, score.Points(0));
        }

        [Test]
        public void NoInstigator_GoesToUnattributed_AndStillCountsInTotal()
        {
            var score = new RunScore(1);

            score.AddDestroyed(-1, BlockMaterial.Wood, 1);

            Assert.AreEqual(0, score.Points(0));
            Assert.AreEqual(BlockMaterialInfo.Points(BlockMaterial.Wood), score.Unattributed);
            Assert.AreEqual(score.Unattributed, score.Total);
        }

        [Test]
        public void Collapse_PaysAChainMultiplier_SoAWeakPointBeatsGrinding()
        {
            var cells = new BlockMaterial[40];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = BlockMaterial.Brick;

            var collapse = new RunScore(1);
            collapse.AddCollapse(0, cells);
            var grind = new RunScore(1);
            for (int i = 0; i < cells.Length; i++)
                grind.AddDestroyed(0, BlockMaterial.Brick, 1);

            // 40 cells → ×3 (1 + 40/20, capped).
            Assert.AreEqual(3 * grind.Total, collapse.Total);
        }

        [Test]
        public void Collapse_CountsInWholeNumbers_RoundingHalvesUp()
        {
            var score = new RunScore(1);
            var cells = new BlockMaterial[3];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = BlockMaterial.Brick;
            score.AddCollapse(0, cells);
            int worth = 3 * BlockMaterialInfo.Points(BlockMaterial.Brick);
            Assert.AreEqual((worth * 23 + 10) / 20, score.Total); // x 1.15, halves up
        }

        [Test]
        public void ChainMultiplier_IsCappedAtThree()
        {
            Assert.AreEqual(1.05f, RunScore.ChainMultiplier(1), 1e-5f);
            Assert.AreEqual(3f, RunScore.ChainMultiplier(500), 1e-5f);
        }

        [Test]
        public void SpeedMultiplier_FullBonusUnderSixtyPercentOfPar_NoneAtOrOverPar()
        {
            Assert.AreEqual(1.5f, RunScore.SpeedMultiplier(30f, 100f), 1e-5f);
            Assert.AreEqual(1.25f, RunScore.SpeedMultiplier(80f, 100f), 1e-5f);
            Assert.AreEqual(1f, RunScore.SpeedMultiplier(100f, 100f), 1e-5f);
            Assert.AreEqual(1f, RunScore.SpeedMultiplier(300f, 100f), 1e-5f);
        }

        [Test]
        public void Penalty_NeverTakesAPlayerBelowZero()
        {
            var score = new RunScore(2);
            score.AddDestroyed(0, BlockMaterial.Glass, 1);

            score.ApplyPenalty(0, 500);

            Assert.AreEqual(0, score.Points(0));
        }

        [Test]
        public void Grade_ByDestroyedFraction()
        {
            Assert.AreEqual(Grade.S, RunScore.GradeFor(0.85f));
            Assert.AreEqual(Grade.A, RunScore.GradeFor(0.6f));
            Assert.AreEqual(Grade.B, RunScore.GradeFor(0.45f));
            Assert.AreEqual(Grade.C, RunScore.GradeFor(0.1f));
            Assert.AreEqual(0, RunScore.Stars(RunScore.GradeFor(0.1f)));
            for (int star = 0; star < RunScore.MaxStars; star++)
            {
                Assert.AreEqual(star + 1, RunScore.Stars(RunScore.GradeFor(RunScore.StarThreshold(star))), $"star {star}");
                Assert.AreEqual(star, RunScore.Stars(RunScore.GradeFor(RunScore.StarThreshold(star) - 0.01f)), $"just short of star {star}");
            }
        }

        [Test]
        public void WithinPar_AtOrUnderPar_NoParNoLimit()
        {
            Assert.IsTrue(RunScore.WithinPar(90f, 90f));
            Assert.IsFalse(RunScore.WithinPar(91f, 90f));
            Assert.IsTrue(RunScore.WithinPar(500f, 0f));
        }
    }
}
