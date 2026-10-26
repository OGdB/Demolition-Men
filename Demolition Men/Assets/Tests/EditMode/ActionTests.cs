using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class ActionTests
    {
        [Test]
        public void Meter_FillsToCapacity_AndSpendsOnlyWhenFull()
        {
            var meter = new ActionMeter(100f);
            meter.Add(60f);
            Assert.IsFalse(meter.TrySpend());
            Assert.AreEqual(60f, meter.Charge);
            meter.Add(80f);
            Assert.IsTrue(meter.Full);
            Assert.AreEqual(100f, meter.Charge);
            Assert.IsTrue(meter.TrySpend());
            Assert.AreEqual(0f, meter.Charge);
        }

        [Test]
        public void Meter_DrainsAfterTheGrace_ButNeverWhenFull()
        {
            var meter = new ActionMeter(100f);
            meter.Add(50f);
            meter.Tick(ActionMeter.IdleGrace - 0.1f);
            Assert.AreEqual(50f, meter.Charge);
            meter.Tick(0.1f + 1f);
            Assert.Less(meter.Charge, 50f);
            Assert.Greater(meter.Charge, 40f);

            meter.Fill();
            meter.Tick(60f);
            Assert.IsTrue(meter.Full);
        }

        [Test]
        public void Meter_AddResetsTheGrace()
        {
            var meter = new ActionMeter(100f);
            meter.Add(30f);
            meter.Tick(ActionMeter.IdleGrace - 0.5f);
            meter.Add(1f);
            meter.Tick(ActionMeter.IdleGrace - 0.5f);
            Assert.AreEqual(31f, meter.Charge);
        }

        [Test]
        public void Points_PayCollapsesTheChainMultiplier()
        {
            Assert.AreEqual(3f, ActionMeter.PointsFor(3, collapse: false));
            Assert.AreEqual(40f * RunScore.ChainMultiplier(40), ActionMeter.PointsFor(40, collapse: true));
            Assert.Greater(ActionMeter.PointsFor(40, collapse: true), ActionMeter.PointsFor(40, collapse: false));
        }

        [Test]
        public void Catalog_HasEveryKindOnce_WithPlayableFrames()
        {
            var ids = new HashSet<string>();
            var kinds = new HashSet<ActionKind>();
            foreach (ActionInfo info in ActionCatalog.All)
            {
                Assert.IsTrue(ids.Add(info.Id), info.Id);
                Assert.IsTrue(kinds.Add(info.Kind), info.Id);
                Assert.Greater(info.Charge, 0f, info.Id);
                Assert.Greater(info.Frames.Length, 0, info.Id);
                Assert.That(info.StrikeFrame, Is.InRange(0, info.Frames.Length - 1), info.Id);
                Assert.AreSame(info, ActionCatalog.Get(info.Kind));
                Assert.AreEqual(info.Kind, ActionCatalog.FromId(info.Id));
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(ActionKind)).Length, kinds.Count);
        }

        [Test]
        public void Loadout_DefaultsToTheBurp_AndKeepsEachPlayersPick()
        {
            var save = new SaveData();
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 0));
            ActionCatalog.Equip(save, 1, ActionKind.ShovelLean);
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 0));
            Assert.AreEqual(ActionKind.ShovelLean, ActionCatalog.Equipped(save, 1));
            ActionCatalog.Equip(save, 0, ActionKind.SiteRadio);
            Assert.AreEqual(ActionKind.SiteRadio, ActionCatalog.Equipped(save, 0));

            save.actions[0] = "no-such-action";
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 0));
            save.actions = null; // a profile saved before actions
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 1));
        }
    }
}
