using System;
using System.Collections.Generic;
using System.Linq;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Ranked;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    /// <summary>The anonymous counters (docs/hit-plan.md F0): what is counted, when, and with which fields.</summary>
    public class CountersTests
    {
        private static readonly DateTime Install = new DateTime(2026, 12, 30, 21, 15, 0);

        private readonly List<CountEvent> _sent = new List<CountEvent>();
        private CounterState _state;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _state = new CounterState();
        }

        private bool Session(DateTime now) => Counters.Session(_state, now, "web", _sent.Add);

        // What went out, as "event" or "event:since".
        private string[] Sent() => _sent.Select(c => c.e == Counters.Day ? $"{c.e}:{c.since}" : c.e).ToArray();

        [Test]
        public void TheFirstSession_CountsTheFirstStartAndDayZero_Once()
        {
            Assert.IsTrue(Session(Install), "the profile is to be saved");
            Assert.AreEqual(new[] { "first_start", "day:0" }, Sent());
            Assert.IsTrue(_sent.All(c => c.p == "web"));

            Assert.IsFalse(Session(Install.AddHours(2)), "later the same day, past midnight UTC or not");
            Assert.AreEqual(2, _sent.Count);
        }

        [Test]
        public void OnlyDays0_1_7_30_AreCounted_EachOnce()
        {
            for (int day = 0; day <= 40; day++)
            {
                Session(Install.Date.AddDays(day).AddHours(9));
                Session(Install.Date.AddDays(day).AddHours(20));
            }
            Assert.AreEqual(new[] { "first_start", "day:0", "day:1", "day:7", "day:30" }, Sent());
        }

        [Test]
        public void ADayIsACalendarDay_NotTwentyFourHours()
        {
            Session(Install); // 21:15
            Session(Install.AddHours(3)); // 00:15 the next day
            Assert.AreEqual(new[] { "first_start", "day:0", "day:1" }, Sent());
        }

        [Test]
        public void ASkippedDay_IsNotCountedLater()
        {
            Session(Install);
            Session(Install.AddDays(2));
            Session(Install.AddDays(8));
            Assert.AreEqual(new[] { "first_start", "day:0" }, Sent());
        }

        [Test]
        public void AClockSetBack_CountsNothing()
        {
            Session(Install.AddDays(1));
            _sent.Clear();
            Assert.IsFalse(Session(Install), "before the install day");
            Assert.IsFalse(Session(Install.AddDays(1)), "the same day again");
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void EveryFinishIsCounted_WithItsLevelStarsAndPlayers()
        {
            Assert.IsFalse(Counters.LevelFinished(_state, "town-03", 2, 1, 0, false, "android", _sent.Add));
            Counters.LevelFinished(_state, "town-03", 3, 2, 1, false, "android", _sent.Add);

            Assert.AreEqual(2, _sent.Count);
            CountEvent coop = _sent[1];
            Assert.AreEqual("level_done android town-03 3 2 1 0", $"{coop.e} {coop.p} {coop.level} {coop.stars} {coop.players} {coop.online} {coop.since}");
            Assert.AreEqual(2, _sent[0].stars);
        }

        [Test]
        public void TheTutorial_IsCountedDoneOnce_AndAsALevelEveryTime()
        {
            Assert.IsTrue(Counters.LevelFinished(_state, "Tutorial", 0, 1, 0, true, "pc", _sent.Add), "the profile is to be saved");
            Assert.IsFalse(Counters.LevelFinished(_state, "Tutorial", 1, 1, 0, true, "pc", _sent.Add), "a first finish again, after a reset");
            Counters.LevelFinished(_state, "Tutorial", 1, 1, 0, false, "pc", _sent.Add);

            Assert.AreEqual(new[] { "tutorial_done", "level_done", "level_done", "level_done" }, Sent());
            Assert.AreEqual("tutorial", _sent[1].level);
        }

        [Test]
        public void ALevelsName_IsItsId_AndOneNameForEveryDailyAndRankedDay()
        {
            Assert.AreEqual("town-01", Counters.LevelName("town-01"));
            Assert.AreEqual("tutorial", Counters.LevelName("Tutorial"));
            Assert.AreEqual("daily", Counters.LevelName(Daily.Id(new DateTime(2026, 11, 17))));
            Assert.AreEqual("ranked", Counters.LevelName(new RankedDay { Day = 20261117, Mode = RankedMode.Coop }.LevelId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("My Test Scene")]
        [TestCase("town_01")]
        [TestCase("a-level-with-a-name-too-long")]
        public void AnIdTheServerWouldRefuse_IsNotCounted(string id)
        {
            Assert.IsNull(Counters.LevelName(id));
            Counters.LevelFinished(_state, id, 1, 1, 0, false, "web", _sent.Add);
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void AShare_IsOneCount()
        {
            Counters.Shared("web", _sent.Add);
            Assert.AreEqual(new[] { "share" }, Sent());
        }

        [Test]
        public void AnInvite_IsOneCount_AndCarriesNothingOfTheRoom()
        {
            Counters.Invited("android", _sent.Add);
            Assert.AreEqual(new[] { "invite" }, Sent());
            // What server/relay's CountersTests posts for it: the platform and no other field set.
            Assert.AreEqual("{\"e\":\"invite\",\"p\":\"android\",\"level\":\"\",\"stars\":0,\"players\":0,\"online\":0,\"since\":0}",
                JsonUtility.ToJson(_sent[0]));
        }

        [Test]
        public void NothingIsSent_FromTheEditor_OnATemporaryProfile_OrWithNoAddress()
        {
            Assert.IsTrue(Counters.MaySend("https://example.org/count", false, false));
            Assert.IsFalse(Counters.MaySend("https://example.org/count", true, false), "the editor: tests, the Clip Lab, bot runs");
            Assert.IsFalse(Counters.MaySend("https://example.org/count", false, true), "Profile.UseTemporary");
            Assert.IsFalse(Counters.MaySend("", false, false), "GameConfig.countersUrl is empty until the owner sets it");
            Assert.IsFalse(Counters.MaySend(null, false, false));
        }

        [Test]
        public void OnTheWire_AnEventIsTheseFieldsAndNoOthers()
        {
            // The texts server/relay's CountersTests posts. A new field needs the server's list
            // (server/relay Counters.cs), the privacy policy and Play's Data safety answers looked at first.
            Counters.LevelFinished(_state, "town-03", 2, 1, 0, false, "android", _sent.Add);
            Assert.AreEqual("{\"e\":\"level_done\",\"p\":\"android\",\"level\":\"town-03\",\"stars\":2,\"players\":1,\"online\":0,\"since\":0}",
                JsonUtility.ToJson(_sent[0]));

            _state.installDay = _state.lastDay = 1;
            Counters.Session(_state, new DateTime(1, 1, 3), "web", _sent.Add);
            Assert.AreEqual("{\"e\":\"day\",\"p\":\"web\",\"level\":\"\",\"stars\":0,\"players\":0,\"online\":0,\"since\":1}",
                JsonUtility.ToJson(_sent[1]));
        }
    }
}
