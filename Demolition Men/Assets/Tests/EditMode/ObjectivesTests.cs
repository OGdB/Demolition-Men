using System.Collections.Generic;
using System.Linq;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class ObjectivesTests
    {
        [Test]
        public void Sheet_IsThreeDifferentObjectives_TheSameForTheSameSeed()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                ObjectiveKind[] sheet = Objectives.Sheet(seed);
                Assert.AreEqual(Objectives.PerSheet, sheet.Length);
                Assert.AreEqual(sheet.Length, sheet.Distinct().Count(), $"seed {seed}");
                CollectionAssert.AreEqual(sheet, Objectives.Sheet(seed));
            }
            var sheets = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
                sheets.Add(string.Join(",", Objectives.Sheet(seed)));
            Assert.Greater(sheets.Count, 10, "the seed picks the sheet");
        }

        [Test]
        public void Sheet_PutsPinnedObjectivesFirst_AndSkipsUnknownIds()
        {
            ObjectiveKind[] sheet = Objectives.Sheet(7, new[] { "beatpar", "nonsense", "beatpar", Objectives.Id(ObjectiveKind.Chain) });
            Assert.AreEqual(ObjectiveKind.BeatPar, sheet[0]);
            Assert.AreEqual(ObjectiveKind.Chain, sheet[1]);
            Assert.AreEqual(Objectives.PerSheet, sheet.Distinct().Count());
        }

        [Test]
        public void ANewSeed_DropsTheOriginalsPinnedSheet()
        {
            var spec = new LevelSpec { seed = 3, objectives = new[] { "wreck" } };
            Assert.IsEmpty(spec.WithSeed(99, "daily-x").objectives);
        }

        [Test]
        public void Tally_CountsThrowsAndHits_NotPickupsOrRevives()
        {
            var tally = new RunTally(2);
            tally.Observe(0, false, 0, 100f);
            tally.Observe(0, false, 3, 100f); // picked up sticks
            tally.Observe(1, false, 0, 100f);
            Assert.IsFalse(tally.Dynamite || tally.Hurt || tally.Armed);
            tally.Observe(0, false, 2, 100f);
            Assert.IsTrue(tally.Dynamite);
            tally.Observe(1, false, 0, 100f); // healed or revived: never less
            Assert.IsFalse(tally.Hurt);
            tally.Observe(1, false, 0, 80f);
            Assert.IsTrue(tally.Hurt);
            tally.Observe(1, true, 0, 80f);
            Assert.IsTrue(tally.Armed);
            tally.Collapsed(12, 0f);
            tally.Collapsed(5, 20f);
            Assert.AreEqual(12, tally.BiggestCollapse);
            tally.Collapsed(5, 20f + RunTally.ChainWindow); // crumbles that follow count with it
            tally.Collapsed(4, 21f + RunTally.ChainWindow);
            Assert.AreEqual(14, tally.BiggestCollapse);

            var client = new RunTally(2);
            client.Restore(tally.Flags, tally.BiggestCollapse);
            Assert.IsTrue(client.Armed && client.Dynamite && client.Hurt);
            Assert.AreEqual(14, client.BiggestCollapse);
        }

        [Test]
        public void Met_NeedsAFinish_AndEachObjectivesCondition()
        {
            var clean = new RunTally(1);
            clean.Collapsed(Objectives.ChainCells, 0f);
            foreach (ObjectiveKind kind in System.Enum.GetValues(typeof(ObjectiveKind)))
            {
                Assert.IsFalse(Objectives.Met(kind, clean, false, 1f, true), $"{kind} without a finish");
                Assert.IsTrue(Objectives.Met(kind, clean, true, 1f, true), kind.ToString());
            }
            var messy = new RunTally(1);
            messy.Observe(0, true, 1, 100f);
            messy.Observe(0, true, 0, 50f);
            messy.Collapsed(Objectives.ChainCells - 1, 0f);
            foreach (ObjectiveKind kind in new[] { ObjectiveKind.Chain, ObjectiveKind.FistsOnly, ObjectiveKind.NoDynamite, ObjectiveKind.NoDamage })
                Assert.IsFalse(Objectives.Met(kind, messy, true, 1f, true), kind.ToString());
            Assert.IsFalse(Objectives.Met(ObjectiveKind.Wreck, clean, true, Objectives.WreckShare - 0.01f, true));
            Assert.IsFalse(Objectives.Met(ObjectiveKind.BeatPar, clean, true, 1f, false));

            var sheet = new[] { ObjectiveKind.FistsOnly, ObjectiveKind.Wreck, ObjectiveKind.NoDamage };
            Assert.AreEqual(0b010, Objectives.MetMask(sheet, messy, true, 1f, true));
            Assert.AreEqual(0b101, Objectives.BlownMask(sheet, messy));
            Assert.AreEqual(0, Objectives.BlownMask(sheet, clean));
        }

        [Test]
        public void EachObjective_PaysOnce_AndFullSheetsCount()
        {
            var save = new SaveData();
            RunReward first = save.RecordRun("town-01", true, Grade.B, 0, 60f, objectivesMet: 0b011);
            Assert.AreEqual(0b011, first.NewObjectives);
            Assert.AreEqual(2 * Objectives.CashEach, first.ObjectiveCash);
            Assert.AreEqual(2 * Objectives.CashEach, save.cash);

            RunReward again = save.RecordRun("town-01", true, Grade.B, 0, 60f, objectivesMet: 0b110);
            Assert.AreEqual(0b100, again.NewObjectives);
            Assert.AreEqual(Objectives.CashEach, again.ObjectiveCash);
            Assert.AreEqual(Objectives.AllDone, save.Level("town-01").objectives);
            Assert.AreEqual(1, save.FullSheets());

            RunReward failed = save.RecordRun("town-02", false, Grade.C, 0, 60f, objectivesMet: 0b111);
            Assert.AreEqual(0, failed.ObjectiveCash, "a failed run does no jobs");
            Assert.AreEqual(1, save.FullSheets());
        }
    }
}
