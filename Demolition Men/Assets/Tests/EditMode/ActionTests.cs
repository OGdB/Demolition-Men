using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;

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
        public void Gate_StartsOnlyOnAFullMeter_OfTheCarriedKind_OneAtATime()
        {
            var gate = new ActionGate();
            var meter = new ActionMeter(50f);
            Assert.IsFalse(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, alive: true, now: 0f), "empty meter");
            meter.Fill();
            Assert.IsFalse(gate.TryStart(meter, ActionKind.SiteRadio, ActionKind.Burp, alive: true, now: 0f), "not carried");
            Assert.IsFalse(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, alive: false, now: 0f), "out of play");
            Assert.IsTrue(meter.Full, "a refusal spends nothing");
            Assert.IsTrue(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, alive: true, now: 1f));
            Assert.AreEqual(0f, meter.Charge, "the start spends it");
            meter.Fill();
            Assert.IsFalse(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, alive: true, now: 2f), "one at a time");
            Assert.IsTrue(meter.Full);
        }

        [Test]
        public void Gate_StrikesOnce_OfTheKindThatStarted_InTime()
        {
            var gate = new ActionGate();
            var meter = new ActionMeter(50f);
            Assert.IsFalse(gate.TryStrike(ActionKind.Burp, 0f), "never started");
            meter.Fill();
            gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, true, 10f);
            Assert.IsFalse(gate.TryStrike(ActionKind.ButtSlam, 11f), "another kind");
            Assert.IsTrue(gate.TryStrike(ActionKind.Burp, 11f));
            Assert.IsFalse(gate.TryStrike(ActionKind.Burp, 11.1f), "once");

            meter.Fill();
            Assert.IsTrue(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, true, 20f), "struck: the next may start");
            Assert.IsFalse(gate.TryStrike(ActionKind.Burp, 20f + ActionGate.StrikeWindow + 0.1f), "too late");
            meter.Fill();
            Assert.IsTrue(gate.TryStart(meter, ActionKind.Burp, ActionKind.Burp, true, 20f + ActionGate.StrikeWindow + 0.2f),
                "an action that never struck stops blocking once its window is over");
        }

        [Test]
        public void Gate_MovesFarClaimsToWhereThePlayerIsSeen()
        {
            var seen = new Vector2(10f, 2f);
            Assert.AreEqual(new Vector2(12f, 2f), ActionGate.Where(new Vector2(12f, 2f), seen));
            Assert.AreEqual(seen, ActionGate.Where(new Vector2(40f, 2f), seen));
        }

        [Test]
        public void Meter_ShowsTheHostsCount_WithinItsCapacity()
        {
            var meter = new ActionMeter(80f);
            meter.Show(30f);
            Assert.AreEqual(30f, meter.Charge);
            meter.Show(500f);
            Assert.IsTrue(meter.Full);
            meter.Show(-3f);
            Assert.AreEqual(0f, meter.Charge);
        }

        [Test]
        public void Calls_OnlyTheHostDecides()
        {
            foreach (ActionPhase phase in System.Enum.GetValues(typeof(ActionPhase)))
            {
                bool clientMay = phase == ActionPhase.Kind || phase == ActionPhase.Start || phase == ActionPhase.Strike;
                Assert.AreEqual(clientMay, ActionCall.Sendable(phase, fromHost: false), phase.ToString());
                Assert.AreEqual(phase != ActionPhase.Strike, ActionCall.Sendable(phase, fromHost: true), phase.ToString());
            }
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
