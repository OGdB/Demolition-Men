using System.Collections;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demolition.Tests
{
    /// <summary>
    /// The bot playtester (roadmap M3): every campaign level is generated, built and played
    /// to the finish by <see cref="BotPilot"/> without errors. Slow: runs at 3x speed, a
    /// few minutes for the whole campaign. Window ▸ General ▸ Test Runner ▸ PlayMode.
    /// <see cref="BotSurvivesLevel"/> plays them as the Clip Lab's bot footage does: mortal, with the
    /// shots' upgrades and an action (each in turn over the campaign) that it fires itself.
    /// </summary>
    public class BotRunTests
    {
        private const float TimeLimit = 360f;
        private const int FootageUpgrades = 5; // content-work/shots/broll-*.json

        private static IEnumerable Levels()
        {
            int count = GameConfig.Instance.Campaign.Count;
            for (int i = 0; i < count; i++)
                yield return i;
        }

        // A fresh profile per run: no shop upgrades from this machine, nothing banked into
        // the developer's real save.
        [SetUp]
        public void SetUp() => Profile.UseTemporary();

        [TearDown]
        public void TearDown()
        {
            Profile.UseDisk();
            LevelBootstrap.AutoPilot = false;
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotFinishesLevel([ValueSource(nameof(Levels))] int index)
        {
            // The level is under test, not the bot's dodging: debris and blasts don't end the run
            // (pits and the camera's edge still do).
            yield return Play(index, invincible: true, "");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotSurvivesLevel([ValueSource(nameof(Levels))] int index)
        {
            ActionInfo action = ActionCatalog.All[index % ActionCatalog.All.Count];
            ActionCatalog.Equip(Profile.Data, 0, action.Kind);
            foreach (UpgradeDefinition upgrade in UpgradeCatalog.All)
                Profile.Data.SetUpgradeLevel(upgrade.Id, Mathf.Min(FootageUpgrades, upgrade.MaxLevel));
            yield return Play(index, invincible: false, $" with {action.Id}");
        }

        private static IEnumerator Play(int index, bool invincible, string with)
        {
            LevelBootstrap.AutoPilot = true;
            LevelSelection.Select(index);
            SceneManager.LoadScene(GameConfig.Instance.levelScene);
            yield return null;

            RunController run = RunController.Current;
            Assert.IsNotNull(run, "the Level scene builds a run");
            run.Players[0].Health.Invincible = invincible;
            float simulated = 0f;
            while (!run.State.IsOver && simulated < TimeLimit)
            {
                Time.timeScale = 3f;
                simulated += Time.deltaTime;
                yield return null;
            }
            Time.timeScale = 1f;

            string id = LevelSelection.Spec.id;
            Assert.AreEqual(RunPhase.Finished, run.State.Phase,
                $"{id}: bot{with} ended {run.State.Phase} after {run.State.Elapsed:0}s at x={run.Players[0].Transform.position.x:0}");
            Debug.Log($"{id}: bot{with} finished in {run.State.Elapsed:0.0}s (par {run.ParTime}), " +
                      $"destruction {run.DestroyedFraction:P0}, score {run.FinalScore}");
        }
    }
}
