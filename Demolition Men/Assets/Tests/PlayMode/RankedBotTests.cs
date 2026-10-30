using System.Collections;
using System.IO;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Ranked;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demolition.Tests
{
    /// <summary>
    /// The honest side of ranked (docs/ranked.md K2): the bot plays every campaign level as a ranked
    /// run, firing its action where BotTactics says to (a different action per level, so all of
    /// them get used), and the server's verifier must pass every log it writes. A failure names the
    /// rule and the event: a slack in RankedRules too tight, or a damage path the log misses.
    /// Slow, like BotRunTests. Window ▸ General ▸ Test Runner ▸ PlayMode.
    /// </summary>
    public class RankedBotTests
    {
        private const float TimeLimit = 360f;

        private static IEnumerable Levels()
        {
            int count = GameConfig.Instance.Campaign.Count;
            for (int i = 0; i < count; i++)
                yield return i;
        }

        [SetUp]
        public void SetUp() => Profile.UseTemporary();

        [TearDown]
        public void TearDown()
        {
            Profile.UseDisk();
            LevelBootstrap.AutoPilot = false;
            LevelBootstrap.Players = 0;
            LevelSelection.Clear();
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotsRankedRun_PassesTheVerifier([ValueSource(nameof(Levels))] int index) => Play(index, 1);

        /// <summary>Two bots on one device: the co-op board's log, with its rejoins.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BotsCoopRun_PassesTheVerifier([ValueSource(nameof(Levels))] int index) => Play(index, 2);

        private static IEnumerator Play(int index, int players)
        {
            CampaignLevel level = GameConfig.Instance.Campaign[index];
            ActionKind action = ActionCatalog.All[index % ActionCatalog.All.Count].Kind;
            ActionCatalog.PickRanked(Profile.Data, 0, action); // not owned: ranked lends it
            if (players > 1)
                ActionCatalog.PickRanked(Profile.Data, 1, ActionCatalog.All[(index + 4) % ActionCatalog.All.Count].Kind);
            LevelBootstrap.AutoPilot = true;
            LevelBootstrap.Players = players;
            Assert.IsTrue(LevelSelection.SelectRanked(new RankedDay { Day = 20261028, Template = level.Spec.id, Seed = level.Spec.seed, Mode = players > 1 ? RankedMode.Coop : RankedMode.Solo }));
            SceneManager.LoadScene(GameConfig.Instance.levelScene);
            yield return null;

            RunController run = RunController.Current;
            Assert.IsNotNull(run, "the Level scene builds a run");
            RunRecorder recorder = RunRecorder.Current;
            Assert.IsNotNull(recorder, "a ranked run is recorded");
            for (int i = 0; i < players; i++)
                run.Players[i].Health.Invincible = true;
            if (players > 1)
                run.Players[1].Transform.gameObject.AddComponent<BotPilot>();
            Assert.AreEqual(action, run.Players[0].Action.Info.Kind, "ranked lends every action");
            Assert.AreEqual(UpgradeCatalog.Value(new SaveData(), UpgradeStat.Speed), run.Players[0].Motor.MaxSpeed, "no upgrades");
            BotPilot bot = run.Players[0].Transform.GetComponent<BotPilot>();
            Assert.IsNotNull(bot);
            float simulated = 0f;
            while (!run.State.IsOver && simulated < TimeLimit)
            {
                Time.timeScale = 3f;
                simulated += Time.deltaTime;
                yield return null;
            }
            Time.timeScale = 1f;

            string id = level.Spec.id;
            Assert.IsTrue(recorder.Complete, $"{id}: the log is complete");
            LevelLayout layout = Object.FindAnyObjectByType<LevelBuilder>().Layout;
            byte[] bytes = recorder.Log.Encode();
            // For the server's replay of real logs (Ranked.Tests/RealLogTests), on another runtime.
            Directory.CreateDirectory("Temp/RankedLogs");
            File.WriteAllBytes($"Temp/RankedLogs/{id}{(players > 1 ? "-coop" : "")}.log", bytes);
            Verdict verdict = RunVerifier.Verify(RunLog.Decode(bytes), layout);
            int strikes = recorder.Log.Events.FindAll(e => e.Kind == LogKind.ActionStrike).Count;
            Debug.Log($"{id}{(players > 1 ? " co-op" : "")} ({action}): {(verdict.Ok ? "passes" : "turned down")}, score {verdict.Score}, " +
                      $"{recorder.Log.Events.Count} events, {strikes} strikes, {bytes.Length} bytes");
            Assert.IsTrue(verdict.Ok, $"{id} ({action}): {verdict.Reason} (event {verdict.Event})");
            Assert.AreEqual(recorder.Log.Score, verdict.Score);
            // With no upgrades the bot may lose a level: BotRunTests tests the bot, here its log still passed.
            if (run.State.Phase != RunPhase.Finished)
                Assert.Inconclusive($"{id} ({action}): the bot ended {run.State.Phase}; the log up to there passes");
        }
    }
}
