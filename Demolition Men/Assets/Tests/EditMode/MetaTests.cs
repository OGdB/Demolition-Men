using Demolition.Ads;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    public class MetaTests
    {
        [Test]
        public void FirstFinish_IsAFirstClear_AndBanksCash()
        {
            var save = new SaveData();
            RunReward reward = save.RecordRun("town-01", true, Grade.B, 10000, 80f);
            Assert.IsTrue(reward.FirstClear);
            Assert.AreEqual(Economy.CashFor(10000, true), reward.Cash);
            Assert.AreEqual(reward.Cash, save.cash);
            Assert.IsTrue(save.IsFinished("town-01"));
            Assert.AreEqual((int)Grade.B, save.Level("town-01").bestGrade);
        }

        [Test]
        public void LaterRuns_KeepTheBest_AndFlagImprovements()
        {
            var save = new SaveData();
            save.RecordRun("town-01", true, Grade.A, 20000, 70f);
            RunReward worse = save.RecordRun("town-01", true, Grade.B, 5000, 90f);
            Assert.IsFalse(worse.FirstClear || worse.NewBestGrade || worse.NewBestScore);
            LevelRecord record = save.Level("town-01");
            Assert.AreEqual(20000, record.bestScore);
            Assert.AreEqual((int)Grade.A, record.bestGrade);
            Assert.AreEqual(70f, record.bestTime);

            RunReward better = save.RecordRun("town-01", true, Grade.S, 30000, 60f);
            Assert.IsTrue(better.NewBestGrade && better.NewBestScore);
            Assert.AreEqual(60f, save.Level("town-01").bestTime);
        }

        [Test]
        public void FailedRun_StillPays_ButUnlocksNothing()
        {
            var save = new SaveData();
            RunReward reward = save.RecordRun("town-02", false, Grade.C, 4000, 30f);
            Assert.Greater(reward.Cash, 0);
            Assert.Less(reward.Cash, Economy.CashFor(4000, true));
            Assert.IsFalse(save.IsFinished("town-02"));
            Assert.AreEqual(1, save.runsPlayed);
            Assert.AreEqual(0, save.runsFinished);
        }

        [Test]
        public void Shop_BuysInOrder_UntilMaxed_AndNeverOverspends()
        {
            var save = new SaveData { cash = 100000 };
            UpgradeDefinition power = UpgradeCatalog.Find("power");
            int spent = 0;
            for (int level = 0; level < power.MaxLevel; level++)
            {
                spent += power.CostOfNext(level);
                Assert.IsTrue(UpgradeCatalog.TryBuy(save, "power"));
            }
            Assert.IsFalse(UpgradeCatalog.TryBuy(save, "power"), "maxed");
            Assert.AreEqual(100000 - spent, save.cash);
            Assert.AreEqual(power.Values[power.MaxLevel], UpgradeCatalog.Value(save, UpgradeStat.Power));

            var broke = new SaveData { cash = 10 };
            Assert.IsFalse(UpgradeCatalog.TryBuy(broke, "health"));
            Assert.AreEqual(10, broke.cash);
            Assert.AreEqual(0, broke.UpgradeLevel("health"));
        }

        [Test]
        public void Catalog_StartsAtThePrefabTuning_AndOnlyGoesUp()
        {
            foreach (UpgradeDefinition upgrade in UpgradeCatalog.All)
            {
                Assert.AreEqual(upgrade.MaxLevel + 1, upgrade.Values.Length, upgrade.Id);
                for (int i = 1; i < upgrade.Values.Length; i++)
                    Assert.Greater(upgrade.Values[i], upgrade.Values[i - 1], upgrade.Id);
                for (int i = 1; i < upgrade.Costs.Length; i++)
                    Assert.Greater(upgrade.Costs[i], upgrade.Costs[i - 1], upgrade.Id);
            }
            var fresh = new SaveData();
            Assert.AreEqual(8f, UpgradeCatalog.Value(fresh, UpgradeStat.Speed));
            Assert.AreEqual(100f, UpgradeCatalog.Value(fresh, UpgradeStat.Health));
            Assert.AreEqual(3f, UpgradeCatalog.Value(fresh, UpgradeStat.Dynamite));
            Assert.AreEqual(1f, UpgradeCatalog.Value(fresh, UpgradeStat.Power));
        }

        [Test]
        public void SaveData_SurvivesAJsonRoundTrip()
        {
            var save = new SaveData { cash = 1234 };
            save.RecordRun("town-03", true, Grade.S, 50000, 99f);
            save.SetUpgradeLevel("speed", 2);
            save.settings.screenShake = false;
            SaveData copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.AreEqual(save.cash, copy.cash);
            Assert.AreEqual(2, copy.UpgradeLevel("speed"));
            Assert.IsFalse(copy.settings.screenShake);
            Assert.AreEqual((int)Grade.S, copy.Level("town-03").bestGrade);
        }

        [Test]
        public void Interstitials_RespectGrace_Spacing_AndRemoval()
        {
            Assert.IsFalse(AdPolicy.InterstitialDue(AdPolicy.GraceRuns, 10, 999f, false), "grace runs");
            Assert.IsFalse(AdPolicy.InterstitialDue(20, AdPolicy.RunsBetweenInterstitials - 1, 999f, false), "too few runs");
            Assert.IsFalse(AdPolicy.InterstitialDue(20, 10, 10f, false), "too soon");
            Assert.IsFalse(AdPolicy.InterstitialDue(20, 10, 999f, true), "removed");
            Assert.IsTrue(AdPolicy.InterstitialDue(20, AdPolicy.RunsBetweenInterstitials, AdPolicy.MinSecondsBetweenInterstitials, false));
        }

        [Test]
        public void HudScale_IsCappedSoTheHudFitsTheScreen()
        {
            var auto = new GameSettings();
            Assert.AreEqual(1f, auto.HudScale(false, 16f / 9f), 0.11f, "desktop 16:9 stays ~100%");
            Assert.AreEqual(1.3f, auto.HudScale(true, 20f / 9f), 1e-4f, "phone AUTO fits a 20:9 screen");
            Assert.AreEqual(1f, auto.HudScale(false, 4f / 3f), 1e-4f, "4:3 keeps the full reference width");
            var big = new GameSettings { hudScale = 1.6f };
            foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 20f / 9f })
            {
                // The canvas expands: 1080 high on wide screens, 1920 wide on narrow ones.
                float canvasWidth = Mathf.Max(1920f, 1080f * aspect) / big.HudScale(false, aspect);
                Assert.GreaterOrEqual(canvasWidth, GameSettings.MinHudWidth - 0.5f, $"aspect {aspect}");
            }
        }

        [Test]
        public void InputFrames_Merge()
        {
            var a = new PlayerInputFrame { Move = new Vector2(1f, 0f), JumpPressed = true, WeaponCycle = 0 };
            var b = new PlayerInputFrame { Move = new Vector2(1f, 0f), AttackHeld = true, WeaponCycle = -1 };
            PlayerInputFrame m = PlayerInputFrame.Merge(a, b);
            Assert.AreEqual(1f, m.Move.magnitude, 1e-4f);
            Assert.IsTrue(m.JumpPressed && m.AttackHeld);
            Assert.AreEqual(-1, m.WeaponCycle);
        }
    }
}
